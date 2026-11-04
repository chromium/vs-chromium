// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>
  /// <see cref="IDashboardServer"/> over the connection of the package to the
  /// server (<see cref="IServerClient"/>): raw requests through the JSON-RPC
  /// connection of the generated protocol client (the parameters of the page
  /// are sent as is), notifications serialized with the attributes of the
  /// generated protocol classes (same JSON as the server sent).
  /// </summary>
  public sealed class DashboardServerAdapter : IDashboardServer, IDisposable {
    private readonly IServerClient _serverClient;
    private volatile string _stoppedReason;

    public DashboardServerAdapter(IServerClient serverClient) {
      _serverClient = serverClient;
      _serverClient.ProcessStarted += ServerClientOnProcessStarted;
      _serverClient.ProcessFatalError += ServerClientOnProcessFatalError;
      _serverClient.IndexStateChanged += OnIndexStateChanged;
      _serverClient.IndexScanStarted += OnIndexScanStarted;
      _serverClient.IndexScanFinished += OnIndexScanFinished;
      _serverClient.IndexLoading += OnIndexLoading;
      _serverClient.IndexLoadingProgress += OnIndexLoadingProgress;
      _serverClient.IndexLoaded += OnIndexLoaded;
      _serverClient.IndexProgress += OnIndexProgress;
    }

    public bool IsRunning => _serverClient.IsServerRunning;
    public string ServerVersion => _serverClient.ServerInfo?.ServerVersion;
    public string StoppedReason => _stoppedReason;

    public event Action<string, JToken> Notification;
    public event EventHandler StateChanged;

    public async Task<JToken> InvokeAsync(string method, JToken parameters, CancellationToken cancellationToken) {
      var client = await _serverClient.GetProtocolClientAsync().ConfigureAwait(false);
      return await client.Rpc.InvokeWithParameterObjectAsync<JToken>(method, parameters, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() {
      _serverClient.ProcessStarted -= ServerClientOnProcessStarted;
      _serverClient.ProcessFatalError -= ServerClientOnProcessFatalError;
      _serverClient.IndexStateChanged -= OnIndexStateChanged;
      _serverClient.IndexScanStarted -= OnIndexScanStarted;
      _serverClient.IndexScanFinished -= OnIndexScanFinished;
      _serverClient.IndexLoading -= OnIndexLoading;
      _serverClient.IndexLoadingProgress -= OnIndexLoadingProgress;
      _serverClient.IndexLoaded -= OnIndexLoaded;
      _serverClient.IndexProgress -= OnIndexProgress;
    }

    private void ServerClientOnProcessStarted(object sender, EventArgs e) {
      _stoppedReason = null;
      StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ServerClientOnProcessFatalError(object sender, ErrorEventArgs e) {
      _stoppedReason = e.GetException()?.Message ?? "error";
      StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Raise(string method, object parameters) {
      Notification?.Invoke(method, parameters == null ? null : JToken.FromObject(parameters));
    }

    private void OnIndexStateChanged(IndexStateChangedParams p) { Raise(ProtocolMethods.IndexStateChanged, p); }
    private void OnIndexScanStarted(OperationStartedParams p) { Raise(ProtocolMethods.IndexScanStarted, p); }
    private void OnIndexScanFinished(ScanFinishedParams p) { Raise(ProtocolMethods.IndexScanFinished, p); }
    private void OnIndexLoading(OperationStartedParams p) { Raise(ProtocolMethods.IndexLoading, p); }
    private void OnIndexLoadingProgress(OperationStartedParams p) { Raise(ProtocolMethods.IndexLoadingProgress, p); }
    private void OnIndexLoaded(FilesLoadedParams p) { Raise(ProtocolMethods.IndexLoaded, p); }
    private void OnIndexProgress(ProgressParams p) { Raise(ProtocolMethods.IndexProgress, p); }
  }
}
