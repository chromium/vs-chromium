// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Threading.Tasks;
using VsChromium.Protocol.Generated;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// The connection to the VsChromium index server: starts the server process
  /// (on first use), exchanges JSON-RPC messages with it (see
  /// protocol/README.md) and raises the notifications it sends. Calling
  /// <see cref="IDisposable.Dispose"/> ensures the server process is terminated
  /// deterministically.
  ///
  /// <para>Threading: all members can be used from any thread. The tasks
  /// returned by <see cref="ProtocolClient"/> complete on thread pool threads,
  /// and the events are raised on thread pool threads (notifications are
  /// raised one at a time, in the order they were sent by the server).</para>
  /// </summary>
  public interface IServerClient : IDisposable {
    /// <summary>
    /// Returns the client to send requests to the server, once the server is
    /// started and initialized ("server/initialize"). The first call starts
    /// the server. The task fails if the server could not be started.
    /// </summary>
    Task<ProtocolClient> GetProtocolClientAsync();

    /// <summary>
    /// True when the server is started and the connection is alive.
    /// </summary>
    bool IsServerRunning { get; }

    /// <summary>
    /// The result of "server/initialize" of the running server (version,
    /// process id), null until the server is started.
    /// </summary>
    InitializeResult ServerInfo { get; }

    /// <summary>
    /// The number of times the server was started (1 after the first start,
    /// incremented by <see cref="RestartAsync"/>).
    /// </summary>
    int StartCount { get; }

    /// <summary>
    /// Stops the server and starts a new one (e.g. to apply settings read when
    /// the server starts). <see cref="ProcessStarted"/> is raised when the new
    /// server is initialized; the end of the old connection is not reported as
    /// an error.
    /// </summary>
    Task RestartAsync();

    /// <summary>
    /// Raised when the server has been started and initialized.
    /// </summary>
    event EventHandler ProcessStarted;

    /// <summary>
    /// Raised when the server could not be started, or when the connection to
    /// the server is lost (e.g. the server crashed).
    /// </summary>
    event EventHandler<ErrorEventArgs> ProcessFatalError;

    /// <summary>"index/stateChanged"</summary>
    event Action<IndexStateChangedParams> IndexStateChanged;
    /// <summary>"index/scanStarted"</summary>
    event Action<OperationStartedParams> IndexScanStarted;
    /// <summary>"index/scanFinished"</summary>
    event Action<ScanFinishedParams> IndexScanFinished;
    /// <summary>"index/loading"</summary>
    event Action<OperationStartedParams> IndexLoading;
    /// <summary>"index/loadingProgress"</summary>
    event Action<OperationStartedParams> IndexLoadingProgress;
    /// <summary>"index/loaded"</summary>
    event Action<FilesLoadedParams> IndexLoaded;
    /// <summary>"index/progress"</summary>
    event Action<ProgressParams> IndexProgress;
  }
}
