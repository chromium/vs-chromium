// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Ipc;
using VsChromium.Core.Logging;
using VsChromium.Protocol.Generated;
using VsChromium.Server.Diagnostics;

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// The JSON-RPC 2.0 connection of the server with its client (see "Transport" in
  /// protocol/README.md): reads the messages of the client (<see cref="Run"/>), dispatches the
  /// requests to an <see cref="IProtocolRequestHandler"/> and writes their responses to a <see
  /// cref="JsonRpcOutput"/> (which also sends the notifications of the server).
  ///
  /// Requests are processed concurrently on the thread pool, except "index/pause" and
  /// "index/resume", which are processed one at a time, in the order they were received (as the
  /// requests with <c>RunOnSequentialQueue</c> of the typed messages protocol). The
  /// "$/cancelRequest" notification cancels the <see cref="CancellationToken"/> of a request: a
  /// cancelled request fails with <see cref="JsonRpcErrorCodes.RequestCancelled"/>.
  /// </summary>
  public sealed class JsonRpcConnection {
    private const string CancelRequestMethod = "$/cancelRequest";

    private readonly IProtocolRequestHandler _handler;
    private readonly JsonRpcOutput _output;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingRequests =
      new ConcurrentDictionary<string, CancellationTokenSource>();
    /// <summary>The last request of the sequential queue (only accessed by the reading thread).</summary>
    private Task _sequentialQueueTail = Task.CompletedTask;

    public JsonRpcConnection(IProtocolRequestHandler handler, JsonRpcOutput output) {
      _handler = handler;
      _output = output;
    }

    /// <summary>
    /// Processes the messages read from <paramref name="input"/> until the end of the stream (the
    /// client closed it), or until a framing error. Pending requests are then cancelled (their
    /// responses are not written if the output is closed). The output must be connected (<see
    /// cref="JsonRpcOutput.Connect"/>).
    /// </summary>
    public void Run(Stream input) {
      if (!_output.IsConnected) {
        throw new InvalidOperationException("The output of the connection must be connected first");
      }
      var reader = new JsonRpcMessageReader(input);
      while (true) {
        byte[] message;
        try {
          message = reader.ReadMessage();
        }
        catch (JsonRpcFramingException e) {
          Logger.LogError(e, "Invalid JSON-RPC message framing: the messages of the client can't be read anymore.");
          WriteError(null, JsonRpcErrorCodes.ParseError, e.Message, null);
          break;
        }
        catch (Exception e) when (e is IOException || e is ObjectDisposedException) {
          Logger.LogError(e, "Error reading the input stream of the JSON-RPC connection.");
          break;
        }

        if (message == null) {
          Logger.LogInfo("End of the input stream of the JSON-RPC connection.");
          break;
        }

        ProcessMessage(message);
      }

      foreach (var request in _pendingRequests.Values) {
        Cancel(request);
      }
    }

    private void ProcessMessage(byte[] message) {
      JsonDocument document;
      try {
        document = JsonDocument.Parse(message);
      }
      catch (JsonException e) {
        Logger.LogError(e, "Invalid JSON-RPC message (JSON parse error).");
        WriteError(null, JsonRpcErrorCodes.ParseError, "Parse error: " + e.Message, null);
        return;
      }

      var disposeDocument = true;
      try {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) {
          WriteInvalidRequest(null, "The message must be a JSON object (batches are not supported)");
          return;
        }

        var hasId = root.TryGetProperty("id", out var id);
        if (hasId && id.ValueKind != JsonValueKind.Number && id.ValueKind != JsonValueKind.String &&
            id.ValueKind != JsonValueKind.Null) {
          WriteInvalidRequest(null, "The \"id\" of a request must be a number or a string");
          return;
        }
        JsonElement? responseId = hasId ? id : null;

        if (!root.TryGetProperty("jsonrpc", out var version) || version.ValueKind != JsonValueKind.String ||
            version.GetString() != "2.0") {
          WriteInvalidRequest(responseId, "The \"jsonrpc\" property must be \"2.0\"");
          return;
        }

        if (!root.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String) {
          // Note: The server doesn't send requests, so it doesn't expect responses either.
          WriteInvalidRequest(responseId, "The \"method\" property must be a string");
          return;
        }
        var method = methodElement.GetString();

        JsonElement? parameters = null;
        if (root.TryGetProperty("params", out var paramsElement)) {
          if (paramsElement.ValueKind != JsonValueKind.Object && paramsElement.ValueKind != JsonValueKind.Array) {
            WriteInvalidRequest(responseId, "The \"params\" property must be an object or an array");
            return;
          }
          parameters = paramsElement;
        }

        if (!hasId) {
          ProcessNotification(method, parameters);
          return;
        }

        // The document is disposed once the request is processed.
        disposeDocument = false;
        StartRequest(new Request(id, method, parameters, document));
      }
      finally {
        if (disposeDocument) {
          document.Dispose();
        }
      }
    }

    private void WriteInvalidRequest(JsonElement? id, string message) {
      if (id == null) {
        Logger.LogWarn("Invalid JSON-RPC message: {0}", message);
      } else {
        Logger.LogWarn("Invalid JSON-RPC request {0}: {1}", id.Value.GetRawText(), message);
      }
      // Note: Notifications (no id) don't get responses, but a message we can't understand at all
      // is reported (with a null id), as for parse errors.
      WriteError(id, JsonRpcErrorCodes.InvalidRequest, "Invalid request: " + message, null);
    }

    private void ProcessNotification(string method, JsonElement? parameters) {
      if (method == CancelRequestMethod) {
        if (parameters is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("id", out var id) &&
            (id.ValueKind == JsonValueKind.Number || id.ValueKind == JsonValueKind.String)) {
          if (_pendingRequests.TryGetValue(GetRequestKey(id), out var cancellation)) {
            Logger.LogInfo("Cancelling request {0}.", id.GetRawText());
            Cancel(cancellation);
          }
        } else {
          Logger.LogWarn("Invalid parameters of notification \"{0}\".", method);
        }
        return;
      }
      // Notifications we don't know are ignored, as required by JSON-RPC (no response).
      Logger.LogInfo("Unknown notification \"{0}\" ignored.", method);
    }

    private void StartRequest(Request request) {
      if (!_pendingRequests.TryAdd(request.Key, request.Cancellation)) {
        request.Document.Dispose();
        Logger.LogWarn("JSON-RPC request id {0} is already used by a pending request.", request.IdText);
        WriteError(null, JsonRpcErrorCodes.InvalidRequest,
          $"Invalid request: the id {request.IdText} is already used by a pending request", null);
        return;
      }

      if (IsSequentialRequest(request.Method)) {
        // Run after the previous sequential requests, in the order they were received.
        _sequentialQueueTail = _sequentialQueueTail
          .ContinueWith(_ => ProcessRequestAsync(request), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default)
          .Unwrap();
      } else {
        Task.Run(() => ProcessRequestAsync(request));
      }
    }

    private static bool IsSequentialRequest(string method) {
      return method == ProtocolMethods.IndexPause || method == ProtocolMethods.IndexResume;
    }

    private async Task ProcessRequestAsync(Request request) {
      var stopwatch = Stopwatch.StartNew();
      try {
        var cancellationToken = request.Cancellation.Token;
        try {
          cancellationToken.ThrowIfCancellationRequested();
          var result = await ProtocolDispatcher.DispatchRequestAsync(_handler, request.Method, request.Parameters, cancellationToken)
            .ConfigureAwait(false);
          // A request cancelled while it was running fails, even if it completed.
          cancellationToken.ThrowIfCancellationRequested();
          WriteResult(request.Id, result);
        }
        catch (Exception e) {
          WriteRequestError(request, e);
        }
      }
      catch (Exception e) {
        // Writing the response failed in an unexpected way.
        Logger.LogError(e, "Error processing JSON-RPC request {0} \"{1}\".", request.IdText, request.Method);
      }
      finally {
        _pendingRequests.TryRemove(request.Key, out _);
        request.Document.Dispose();
        stopwatch.Stop();
        if (ProtocolDispatcher.IsRequest(request.Method)) {
          OperationTimings.Record(OperationTimings.RequestPrefix + request.Method, stopwatch.Elapsed);
        }
        Logger.LogInfo("Request {0} \"{1}\" took {2:n0} msec to execute.", request.IdText, request.Method,
          stopwatch.ElapsedMilliseconds);
      }
    }

    private void WriteRequestError(Request request, Exception error) {
      switch (error) {
        case ProtocolException e:
          Logger.LogInfo("Request {0} \"{1}\" failed with protocol error {2}: {3}", request.IdText, request.Method, e.Code, e.Message);
          WriteError(request.Id, e.Code, e.Message, null);
          break;
        case OperationCanceledException _:
          Logger.LogInfo("Request {0} \"{1}\" has been canceled.", request.IdText, request.Method);
          WriteError(request.Id, JsonRpcErrorCodes.RequestCancelled, "Request cancelled", null);
          break;
        case RecoverableErrorException e:
          Logger.LogInfo("Request {0} \"{1}\" generated a recoverable error: {2}.", request.IdText, request.Method, e.Message);
          WriteError(request.Id, JsonRpcErrorCodes.RecoverableError, e.Message, null);
          break;
        default:
          Logger.LogError(error, "Error executing request {0} \"{1}\".", request.IdText, request.Method);
          WriteError(request.Id, JsonRpcErrorCodes.InternalError, error.Message, JsonRpcErrors.CreateErrorInfo(error));
          break;
      }
    }

    private void WriteResult(JsonElement id, JsonElement? result) {
      using (var message = new JsonRpcMessageBuffer()) {
        using (var json = new Utf8JsonWriter(message, JsonRpcOutput.WriterOptions)) {
          json.WriteStartObject();
          json.WriteString("jsonrpc", "2.0");
          json.WritePropertyName("id");
          id.WriteTo(json);
          json.WritePropertyName("result");
          if (result is { } value) {
            value.WriteTo(json);
          } else {
            json.WriteNullValue();
          }
          json.WriteEndObject();
        }
        Write(message);
      }
    }

    private void WriteError(JsonElement? id, int code, string errorMessage, ErrorInfo data) {
      using (var message = new JsonRpcMessageBuffer()) {
        using (var json = new Utf8JsonWriter(message, JsonRpcOutput.WriterOptions)) {
          json.WriteStartObject();
          json.WriteString("jsonrpc", "2.0");
          json.WritePropertyName("id");
          if (id is { } value) {
            value.WriteTo(json);
          } else {
            json.WriteNullValue();
          }
          json.WritePropertyName("error");
          json.WriteStartObject();
          json.WriteNumber("code", code);
          json.WriteString("message", errorMessage ?? "");
          if (data != null) {
            json.WritePropertyName("data");
            JsonSerializer.Serialize(json, data, ProtocolJsonContext.Default.ErrorInfo);
          }
          json.WriteEndObject();
          json.WriteEndObject();
        }
        Write(message);
      }
    }

    private void Write(JsonRpcMessageBuffer message) {
      _output.Write(message);
    }

    private static void Cancel(CancellationTokenSource cancellation) {
      try {
        cancellation.Cancel();
      }
      catch (Exception e) {
        // Exceptions of callbacks registered on the token.
        Logger.LogError(e, "Error cancelling a request.");
      }
    }

    /// <summary>
    /// The key of a request id in <see cref="_pendingRequests"/>: numbers and strings are different
    /// ids (1 and "1").
    /// </summary>
    private static string GetRequestKey(JsonElement id) {
      return id.ValueKind == JsonValueKind.String ? "s:" + id.GetString() : "n:" + id.GetRawText();
    }

    private sealed class Request {
      public Request(JsonElement id, string method, JsonElement? parameters, JsonDocument document) {
        Id = id;
        Method = method;
        Parameters = parameters;
        Document = document;
        Key = GetRequestKey(id);
        IdText = id.GetRawText();
        // Note: Not disposed: no timer nor linked token (disposing would race with "$/cancelRequest").
        Cancellation = new CancellationTokenSource();
      }

      /// <summary>The id of the request (number, string or null), echoed in the response.</summary>
      public JsonElement Id { get; }
      public string Method { get; }
      public JsonElement? Parameters { get; }
      /// <summary>The document of the request (owns the memory of <see cref="Id"/> and <see cref="Parameters"/>).</summary>
      public JsonDocument Document { get; }
      public string Key { get; }
      public string IdText { get; }
      public CancellationTokenSource Cancellation { get; }
    }
  }
}
