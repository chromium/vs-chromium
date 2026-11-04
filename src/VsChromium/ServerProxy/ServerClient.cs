// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.Protocol.Generated;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// Implementation of <see cref="IServerClient"/> with StreamJsonRpc: LSP
  /// framing ("Content-Length" headers) over the standard input/output of the
  /// server process started by <see cref="IServerProcessLauncher"/>.
  /// </summary>
  [Export(typeof(IServerClient))]
  [Export(typeof(IPackagePostDispose))]
  public class ServerClient : IServerClient, IPackagePostDispose {
    /// <summary>
    /// Version of the protocol implemented by this client (see
    /// "server/initialize").
    /// </summary>
    public const int ProtocolVersion = 1;
    public const string ClientName = "Visual Studio";
    /// <summary>
    /// Header line written before the first message (see <see cref="StartAsync"/>).
    /// </summary>
    private const string PreambleHeader = "X-VsChromium-Preamble: ignored\r\n";

    private readonly IServerProcessLauncher _serverProcessLauncher;
    private readonly object _lock = new object();
    private Task<ProtocolClient> _startTask;
    private JsonRpc _rpc;
    private volatile bool _isServerRunning;
    private volatile bool _disposed;
    private volatile InitializeResult _serverInfo;
    private int _startCount;

    [ImportingConstructor]
    public ServerClient(IServerProcessLauncher serverProcessLauncher) {
      _serverProcessLauncher = serverProcessLauncher;
    }

    public event EventHandler ProcessStarted;
    public event EventHandler<ErrorEventArgs> ProcessFatalError;
    public event Action<IndexStateChangedParams> IndexStateChanged;
    public event Action<OperationStartedParams> IndexScanStarted;
    public event Action<ScanFinishedParams> IndexScanFinished;
    public event Action<OperationStartedParams> IndexLoading;
    public event Action<OperationStartedParams> IndexLoadingProgress;
    public event Action<FilesLoadedParams> IndexLoaded;
    public event Action<ProgressParams> IndexProgress;

    public bool IsServerRunning => _isServerRunning;

    public InitializeResult ServerInfo => _serverInfo;

    public int StartCount => _startCount;

    public async Task RestartAsync() {
      JsonRpc rpc;
      lock (_lock) {
        if (_disposed) {
          throw new ObjectDisposedException(nameof(ServerClient));
        }
        rpc = _rpc;
        // Note: The disconnection of the old connection is ignored (see
        // RpcOnDisconnected): it is not the current connection anymore.
        _rpc = null;
        _startTask = null;
      }
      _isServerRunning = false;
      _serverInfo = null;
      Logger.LogInfo("Restarting the index server.");
      try {
        rpc?.Dispose();
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error closing the connection to the server.");
      }
      _serverProcessLauncher.StopServer();
      await GetProtocolClientAsync().ConfigureAwait(false);
    }

    public Task<ProtocolClient> GetProtocolClientAsync() {
      lock (_lock) {
        if (_disposed) {
          throw new ObjectDisposedException(nameof(ServerClient));
        }
        if (_startTask == null) {
          // Start the server on the thread pool (never on the UI thread).
          _startTask = Task.Run(() => StartAsync());
        }
        // The task is intentionally shared (created once) and runs on the
        // thread pool, so it cannot deadlock the UI thread.
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        return _startTask;
#pragma warning restore VSTHRD003
      }
    }

    public void Dispose() {
      JsonRpc rpc;
      lock (_lock) {
        if (_disposed)
          return;
        _disposed = true;
        rpc = _rpc;
      }
      _isServerRunning = false;

      // Closing the connection closes the standard input of the server, which
      // makes it exit; the launcher waits for the exit (and kills the server
      // if needed).
      try {
        rpc?.Dispose();
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error closing the connection to the server.");
      }
      _serverProcessLauncher.Dispose();
    }

    /// <summary>
    /// Runs before the disposal of the launcher (priority 0), so that the
    /// end of the connection is not reported as an error.
    /// </summary>
    int IPackagePostDispose.Priority => 1;

    void IPackagePostDispose.Run(IVisualStudioPackage package) {
      Dispose();
    }

    private async Task<ProtocolClient> StartAsync() {
      try {
        var serverProcess = await _serverProcessLauncher.StartServerAsync().ConfigureAwait(false);

        // On .NET Framework, Process.Start writes the preamble of
        // Console.InputEncoding to the standard input of the server (a UTF-8
        // BOM when the code page of the console is 65001, e.g. in test hosts),
        // which would corrupt the "Content-Length" header of the first message.
        // The first message starts with an extra header line, ignored by the
        // server ("other headers are ignored"), that absorbs the BOM if any.
        var preamble = Encoding.ASCII.GetBytes(PreambleHeader);
        await serverProcess.InputStream.WriteAsync(preamble, 0, preamble.Length).ConfigureAwait(false);
        await serverProcess.InputStream.FlushAsync().ConfigureAwait(false);

        // Note: The server ignores the "Content-Type" header.
        var handler = new HeaderDelimitedMessageHandler(
          serverProcess.InputStream,
          serverProcess.OutputStream,
          new JsonMessageFormatter());
        var rpc = new JsonRpc(handler);
        rpc.AddLocalRpcTarget<IProtocolNotifications>(new NotificationsTarget(this), null);
        rpc.Disconnected += RpcOnDisconnected;
        lock (_lock) {
          if (_disposed) {
            rpc.Dispose();
            throw new ObjectDisposedException(nameof(ServerClient));
          }
          _rpc = rpc;
        }
        rpc.StartListening();

        var client = new ProtocolClient(rpc);
        var result = await client.ServerInitializeAsync(new InitializeParams {
          ClientName = ClientName,
          ClientVersion = GetClientVersion(),
          ProtocolVersion = ProtocolVersion,
        }).ConfigureAwait(false);
        Logger.LogInfo("Connected to server (version {0}, protocol version {1}, pid={2}).",
          result.ServerVersion, result.ProtocolVersion, result.ServerProcessId);

        _serverInfo = result;
        Interlocked.Increment(ref _startCount);
        _isServerRunning = true;
        OnProcessStarted();
        return client;
      }
      catch (Exception e) {
        Logger.LogError(e, "Error starting the index server.");
        _isServerRunning = false;
        if (!_disposed) {
          OnProcessFatalError(new ErrorEventArgs(e));
        }
        throw;
      }
    }

    private static string GetClientVersion() {
      return Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }

    private void RpcOnDisconnected(object sender, JsonRpcDisconnectedEventArgs e) {
      lock (_lock) {
        if (!_disposed && !ReferenceEquals(sender, _rpc)) {
          // A previous connection (the server was restarted).
          Logger.LogInfo("Connection to the previous server closed.");
          return;
        }
      }
      var wasRunning = _isServerRunning;
      _isServerRunning = false;
      if (_disposed) {
        Logger.LogInfo("Connection to the server closed.");
        return;
      }

      Logger.LogError(e.Exception, "Connection to the server lost: {0} ({1}).", e.Description, e.Reason);
      if (wasRunning) {
        var error = e.Exception ?? new InvalidOperationException(
          string.Format("The connection to the index server was lost: {0}", e.Description));
        OnProcessFatalError(new ErrorEventArgs(error));
      }
    }

    protected virtual void OnProcessStarted() {
      ProcessStarted?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnProcessFatalError(ErrorEventArgs e) {
      ProcessFatalError?.Invoke(this, e);
    }

    private static void Raise<T>(Action<T> handler, T parameters) {
      if (handler != null) {
        Logger.WrapActionInvocation(() => handler(parameters));
      }
    }

    /// <summary>
    /// Receives the notifications of the server (on a thread pool thread, one
    /// at a time, in order) and raises the corresponding events.
    /// </summary>
    private class NotificationsTarget : IProtocolNotifications {
      private readonly ServerClient _client;

      public NotificationsTarget(ServerClient client) {
        _client = client;
      }

      public void IndexStateChanged(IndexStateChangedParams parameters) {
        Logger.LogInfo("Notification \"{0}\" received from server: {1}.", ProtocolMethods.IndexStateChanged, parameters.Status);
        Raise(_client.IndexStateChanged, parameters);
      }

      public void IndexScanStarted(OperationStartedParams parameters) {
        Logger.LogInfo("Notification \"{0}\" received from server.", ProtocolMethods.IndexScanStarted);
        Raise(_client.IndexScanStarted, parameters);
      }

      public void IndexScanFinished(ScanFinishedParams parameters) {
        Logger.LogInfo("Notification \"{0}\" received from server.", ProtocolMethods.IndexScanFinished);
        Raise(_client.IndexScanFinished, parameters);
      }

      public void IndexLoading(OperationStartedParams parameters) {
        Logger.LogInfo("Notification \"{0}\" received from server.", ProtocolMethods.IndexLoading);
        Raise(_client.IndexLoading, parameters);
      }

      public void IndexLoadingProgress(OperationStartedParams parameters) {
        Logger.LogInfo("Notification \"{0}\" received from server.", ProtocolMethods.IndexLoadingProgress);
        Raise(_client.IndexLoadingProgress, parameters);
      }

      public void IndexLoaded(FilesLoadedParams parameters) {
        Logger.LogInfo("Notification \"{0}\" received from server.", ProtocolMethods.IndexLoaded);
        Raise(_client.IndexLoaded, parameters);
      }

      public void IndexProgress(ProgressParams parameters) {
        // Not logged: too noisy.
        Raise(_client.IndexProgress, parameters);
      }
    }
  }
}
