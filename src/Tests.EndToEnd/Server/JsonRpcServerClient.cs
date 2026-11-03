// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VsChromium.Tests.EndToEnd.Server;

/// <summary>
/// An error response returned by the server. <see cref="Error"/> is the JSON-RPC error object
/// (code, message, data).
/// </summary>
public sealed class ServerErrorException(string message, JsonObject error) : Exception(message) {
  public JsonObject Error { get; } = error;

  /// <summary>The JSON-RPC error code.</summary>
  public int Code => (int?)Error["code"] ?? 0;
}

/// <summary>A response from the server (the result of a request), with the time it took (measured by the client).</summary>
public sealed record TimedResponse(JsonObject Data, TimeSpan Elapsed, int ResponseSizeBytes);

/// <summary>
/// Client for VsChromium.Server.exe running in "--jsonrpc" mode: JSON-RPC 2.0 over stdin/stdout,
/// with the framing of the Language Server Protocol ("Content-Length" headers). Used as a black box:
/// it only depends on the protocol (protocol/*.tsp).
/// </summary>
public sealed class JsonRpcServerClient : IAsyncDisposable {
  private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

  private readonly Process _process;
  private readonly Stream _input;
  private readonly ConcurrentDictionary<long, Pending> _pending = new();
  private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly Thread _readerThread;
  private readonly object _writeLock = new();
  private readonly object _notificationsLock = new();
  private readonly List<Action<string, JsonObject>> _notificationListeners = new();
  private readonly StringBuilder _stderr = new();
  private long _nextRequestId;

  private sealed record Pending(string Method, TaskCompletionSource<TimedResponse> Completion, Stopwatch Stopwatch);

  private JsonRpcServerClient(string serverPath) {
    _process = Process.Start(new ProcessStartInfo {
      FileName = serverPath,
      Arguments = "--jsonrpc",
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
    }) ?? throw new InvalidOperationException($"Cannot start server \"{serverPath}\"");
    _input = _process.StandardInput.BaseStream;

    _process.ErrorDataReceived += (_, e) => {
      if (e.Data != null) {
        lock (_stderr) { _stderr.AppendLine(e.Data); }
      }
    };
    _process.BeginErrorReadLine();

    _readerThread = new Thread(ReadLoop) { IsBackground = true, Name = "JsonRpcServerClient reader" };
    _readerThread.Start();
  }

  /// <summary>The server process (e.g. to sample its CPU and memory usage from outside).</summary>
  public Process Process => _process;

  /// <summary>The result of the "server/initialize" request.</summary>
  public JsonObject InitializeResult { get; private set; } = new();

  /// <summary>Starts the server and sends the "server/initialize" request.</summary>
  public static async Task<JsonRpcServerClient> StartAsync(string serverPath) {
    if (!File.Exists(serverPath)) {
      throw new FileNotFoundException($"Server executable not found: \"{serverPath}\". Build the solution first, " +
                                      "or set VSCHROMIUM_SERVER_PATH.", serverPath);
    }
    var client = new JsonRpcServerClient(serverPath);
    try {
      var response = await client.RequestAsync(ServerProtocol.Initialize(), TimeSpan.FromSeconds(30));
      client.InitializeResult = response.Data;
    } catch (Exception e) {
      await client.DisposeAsync();
      throw new InvalidOperationException($"Server did not start properly. Stderr: {client.Stderr}", e);
    }
    return client;
  }

  public string Stderr {
    get { lock (_stderr) { return _stderr.ToString(); } }
  }

  /// <summary>Registers a listener for the notifications of the server (method, params).</summary>
  public IDisposable OnNotification(Action<string, JsonObject> listener) {
    lock (_notificationsLock) { _notificationListeners.Add(listener); }
    return new Unsubscriber(() => { lock (_notificationsLock) { _notificationListeners.Remove(listener); } });
  }

  /// <summary>
  /// Waits for the first notification <paramref name="method"/> (e.g. "index/loaded") received
  /// after <paramref name="afterSubscribe"/> is called, and returns its parameters.
  /// </summary>
  public async Task<JsonObject> WaitForNotificationAsync(string method, TimeSpan timeout, Func<Task>? afterSubscribe = null) {
    var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var subscription = OnNotification((m, p) => {
      if (m == method) {
        tcs.TrySetResult(p);
      }
    });
    if (afterSubscribe != null) {
      await afterSubscribe();
    }
    var completed = await Task.WhenAny(tcs.Task, _exited.Task, Task.Delay(timeout));
    if (completed != tcs.Task) {
      throw new TimeoutException($"Notification {method} not received within {timeout} (server exited: {_exited.Task.IsCompleted}).");
    }
    return await tcs.Task;
  }

  /// <summary>
  /// Sends a request and returns its result (an empty object for requests without result). Throws
  /// <see cref="ServerErrorException"/> on error responses.
  /// </summary>
  public Task<TimedResponse> RequestAsync(ServerRequest request, TimeSpan? timeout = null) =>
    RequestAsync(request, out _, timeout);

  /// <summary>Same as <see cref="RequestAsync(ServerRequest, TimeSpan?)"/>, also returns the id of the request.</summary>
  public Task<TimedResponse> RequestAsync(ServerRequest request, out long requestId, TimeSpan? timeout = null) {
    requestId = Interlocked.Increment(ref _nextRequestId);
    return SendRequestAsync(requestId, request, timeout ?? DefaultTimeout);
  }

  /// <summary>Sends a notification (e.g. "$/cancelRequest").</summary>
  public void Notify(string method, JsonObject parameters) {
    Write(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = parameters }, null);
  }

  /// <summary>Sends "$/cancelRequest" for request <paramref name="requestId"/>.</summary>
  public void CancelRequest(long requestId) => Notify("$/cancelRequest", new JsonObject { ["id"] = requestId });

  private async Task<TimedResponse> SendRequestAsync(long requestId, ServerRequest request, TimeSpan timeout) {
    var message = new JsonObject {
      ["jsonrpc"] = "2.0",
      ["id"] = requestId,
      ["method"] = request.Method,
      ["params"] = request.Params.DeepClone(),
    };
    var pending = new Pending(request.Method, new TaskCompletionSource<TimedResponse>(TaskCreationOptions.RunContinuationsAsynchronously), new Stopwatch());
    _pending[requestId] = pending;
    Write(message, pending.Stopwatch);
    var completed = await Task.WhenAny(pending.Completion.Task, _exited.Task, Task.Delay(timeout));
    if (completed != pending.Completion.Task) {
      _pending.TryRemove(requestId, out _);
      throw new TimeoutException($"No response to {request.Method} (server exited: {_exited.Task.IsCompleted}). Stderr: {Stderr}");
    }
    return await pending.Completion.Task;
  }

  private void Write(JsonObject message, Stopwatch? stopwatch) {
    var content = Encoding.UTF8.GetBytes(message.ToJsonString());
    var header = Encoding.ASCII.GetBytes($"Content-Length: {content.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n");
    lock (_writeLock) {
      stopwatch?.Start();
      _input.Write(header);
      _input.Write(content);
      _input.Flush();
    }
  }

  private void ReadLoop() {
    try {
      var output = new BufferedStream(_process.StandardOutput.BaseStream, 64 * 1024);
      while (ReadMessage(output) is { } content) {
        var message = JsonNode.Parse(content)!.AsObject();
        if (message.TryGetPropertyValue("id", out var idNode) && idNode != null) {
          var requestId = idNode.GetValue<long>();
          if (_pending.TryRemove(requestId, out var pending)) {
            pending.Stopwatch.Stop();
            if (message["error"] is JsonObject error) {
              pending.Completion.TrySetException(new ServerErrorException(
                $"{pending.Method}: {(string?)error["message"] ?? "Server error"} ({(int?)error["code"]})", error));
            } else {
              var result = message["result"] as JsonObject ?? new JsonObject();
              pending.Completion.TrySetResult(new TimedResponse(result, pending.Stopwatch.Elapsed, content.Length));
            }
          }
          continue;
        }
        var method = (string?)message["method"];
        if (method != null) {
          var parameters = message["params"] as JsonObject ?? new JsonObject();
          List<Action<string, JsonObject>> listeners;
          lock (_notificationsLock) { listeners = _notificationListeners.ToList(); }
          foreach (var listener in listeners) {
            listener(method, parameters);
          }
        }
      }
    } catch (Exception e) when (e is IOException or ObjectDisposedException or JsonException or InvalidDataException) {
      // Stream closed or corrupted: treated as server exit below.
    } finally {
      _exited.TrySetResult();
      foreach (var pending in _pending.Values) {
        pending.Completion.TrySetException(new InvalidOperationException($"Server exited. Stderr: {Stderr}"));
      }
    }
  }

  /// <summary>Reads a message framed with LSP headers, null at the end of the stream.</summary>
  private static byte[]? ReadMessage(Stream stream) {
    var contentLength = -1;
    var line = new StringBuilder();
    while (true) {
      line.Clear();
      while (true) {
        var b = stream.ReadByte();
        if (b < 0) {
          if (line.Length == 0 && contentLength < 0) {
            return null;
          }
          throw new InvalidDataException("Unexpected end of stream in message headers");
        }
        if (b == '\n') {
          break;
        }
        if (b != '\r') {
          line.Append((char)b);
        }
      }
      if (line.Length == 0) {
        break;
      }
      var text = line.ToString();
      var separator = text.IndexOf(':');
      if (separator > 0 && text[..separator].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) {
        contentLength = int.Parse(text[(separator + 1)..].Trim(), CultureInfo.InvariantCulture);
      }
    }
    if (contentLength < 0) {
      throw new InvalidDataException("Missing Content-Length header");
    }
    var content = new byte[contentLength];
    stream.ReadExactly(content);
    return content;
  }

  public async ValueTask DisposeAsync() {
    try {
      if (!_process.HasExited) {
        _process.StandardInput.Close(); // The server exits when its input is closed.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
          await _process.WaitForExitAsync(cts.Token);
        } catch (OperationCanceledException) {
          _process.Kill(entireProcessTree: true);
        }
      }
    } catch (InvalidOperationException) {
      // Process already gone.
    }
    _readerThread.Join(TimeSpan.FromSeconds(5));
    _process.Dispose();
  }

  private sealed class Unsubscriber(Action action) : IDisposable {
    public void Dispose() => action();
  }
}
