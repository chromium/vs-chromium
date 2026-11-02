// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VsChromium.Tests.EndToEnd.Server;

/// <summary>An error response returned by the server.</summary>
public sealed class ServerErrorException(string message, JsonObject error) : Exception(message) {
  public JsonObject Error { get; } = error;
}

/// <summary>A response from the server, with the time it took (measured by the client).</summary>
public sealed record TimedResponse(JsonObject Data, TimeSpan Elapsed, int ResponseSizeBytes);

/// <summary>
/// Client for VsChromium.Server.exe running in "--stdio" mode (one JSON message per line on
/// stdin/stdout), used as a black box: it only depends on the JSON protocol, so it works with any
/// version of the server (the current one and the ported one).
/// </summary>
public sealed class StdioServerClient : IAsyncDisposable {
  private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

  private readonly Process _process;
  private readonly ConcurrentDictionary<long, Pending> _pending = new();
  private readonly TaskCompletionSource _hello = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly Thread _readerThread;
  private readonly object _writeLock = new();
  private readonly object _eventsLock = new();
  private readonly List<Action<JsonObject>> _eventListeners = new();
  private readonly StringBuilder _stderr = new();
  private long _nextRequestId = 2; // 1 is the "hello" response

  private sealed record Pending(TaskCompletionSource<TimedResponse> Completion, Stopwatch Stopwatch);

  private StdioServerClient(string serverPath) {
    _process = Process.Start(new ProcessStartInfo {
      FileName = serverPath,
      Arguments = "--stdio",
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      StandardInputEncoding = new UTF8Encoding(false),
      StandardOutputEncoding = new UTF8Encoding(false),
    }) ?? throw new InvalidOperationException($"Cannot start server \"{serverPath}\"");

    _process.ErrorDataReceived += (_, e) => {
      if (e.Data != null) {
        lock (_stderr) { _stderr.AppendLine(e.Data); }
      }
    };
    _process.BeginErrorReadLine();

    _readerThread = new Thread(ReadLoop) { IsBackground = true, Name = "StdioServerClient reader" };
    _readerThread.Start();
  }

  /// <summary>The server process (e.g. to sample its CPU and memory usage from outside).</summary>
  public Process Process => _process;

  /// <summary>Starts the server and waits until it is ready (i.e. has sent the "hello" response).</summary>
  public static async Task<StdioServerClient> StartAsync(string serverPath) {
    if (!File.Exists(serverPath)) {
      throw new FileNotFoundException($"Server executable not found: \"{serverPath}\". Build the solution first, " +
                                      "or set VSCHROMIUM_SERVER_PATH.", serverPath);
    }
    var client = new StdioServerClient(serverPath);
    var completed = await Task.WhenAny(client._hello.Task, client._exited.Task, Task.Delay(TimeSpan.FromSeconds(30)));
    if (completed != client._hello.Task) {
      await client.DisposeAsync();
      throw new InvalidOperationException($"Server did not start properly. Stderr: {client.Stderr}");
    }
    return client;
  }

  public string Stderr {
    get { lock (_stderr) { return _stderr.ToString(); } }
  }

  /// <summary>Registers a listener for server events (the "data" object of each IpcEvent).</summary>
  public IDisposable OnEvent(Action<JsonObject> listener) {
    lock (_eventsLock) { _eventListeners.Add(listener); }
    return new Unsubscriber(() => { lock (_eventsLock) { _eventListeners.Remove(listener); } });
  }

  /// <summary>Waits for the first event of the given type (e.g. "SearchEngineFilesLoaded").</summary>
  public async Task<JsonObject> WaitForEventAsync(string eventType, TimeSpan timeout, Func<Task>? afterSubscribe = null) {
    var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var subscription = OnEvent(e => {
      if ((string?)e["$type"] == eventType) {
        tcs.TrySetResult(e);
      }
    });
    if (afterSubscribe != null) {
      await afterSubscribe();
    }
    var completed = await Task.WhenAny(tcs.Task, _exited.Task, Task.Delay(timeout));
    if (completed != tcs.Task) {
      throw new TimeoutException($"Event {eventType} not received within {timeout} (server exited: {_exited.Task.IsCompleted}).");
    }
    return await tcs.Task;
  }

  /// <summary>
  /// Sends a typed request (<paramref name="data"/> must contain "$type", e.g. "SearchCodeRequest")
  /// and returns the typed response data. Throws <see cref="ServerErrorException"/> on error responses.
  /// </summary>
  public async Task<TimedResponse> RequestAsync(JsonObject data, TimeSpan? timeout = null) {
    var requestId = Interlocked.Increment(ref _nextRequestId);
    var request = new JsonObject {
      ["$type"] = "IpcRequest",
      ["requestId"] = requestId,
      ["protocol"] = "typed-message",
      ["data"] = data,
    };
    var pending = new Pending(new TaskCompletionSource<TimedResponse>(TaskCreationOptions.RunContinuationsAsynchronously), new Stopwatch());
    _pending[requestId] = pending;
    var line = request.ToJsonString();
    lock (_writeLock) {
      pending.Stopwatch.Start();
      _process.StandardInput.Write(line);
      _process.StandardInput.Write('\n');
      _process.StandardInput.Flush();
    }
    var completed = await Task.WhenAny(pending.Completion.Task, _exited.Task, Task.Delay(timeout ?? DefaultTimeout));
    if (completed != pending.Completion.Task) {
      _pending.TryRemove(requestId, out _);
      throw new TimeoutException($"No response to {(string?)data["$type"]} (server exited: {_exited.Task.IsCompleted}). Stderr: {Stderr}");
    }
    return await pending.Completion.Task;
  }

  private void ReadLoop() {
    try {
      string? line;
      while ((line = _process.StandardOutput.ReadLine()) != null) {
        if (line.Length == 0) {
          continue;
        }
        var message = JsonNode.Parse(line)!.AsObject();
        var protocol = (string?)message["protocol"];
        if (protocol == "hello") {
          _hello.TrySetResult();
          continue;
        }
        var data = message["data"]?.AsObject() ?? new JsonObject();
        if ((string?)message["$type"] == "IpcEvent") {
          List<Action<JsonObject>> listeners;
          lock (_eventsLock) { listeners = _eventListeners.ToList(); }
          foreach (var listener in listeners) {
            listener(data);
          }
          continue;
        }
        var requestId = message["requestId"]!.GetValue<long>();
        if (_pending.TryRemove(requestId, out var pending)) {
          pending.Stopwatch.Stop();
          if ((string?)data["$type"] == "ErrorResponse") {
            pending.Completion.TrySetException(new ServerErrorException((string?)data["message"] ?? "Server error", data));
          } else {
            pending.Completion.TrySetResult(new TimedResponse(data, pending.Stopwatch.Elapsed, Encoding.UTF8.GetByteCount(line)));
          }
        }
      }
    } catch (Exception e) when (e is IOException or ObjectDisposedException or JsonException) {
      // Stream closed or corrupted: treated as server exit below.
    } finally {
      _exited.TrySetResult();
      foreach (var pending in _pending.Values) {
        pending.Completion.TrySetException(new InvalidOperationException($"Server exited. Stderr: {Stderr}"));
      }
    }
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
