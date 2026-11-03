// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Package {
  [Export(typeof(IPackagePostInitializer))]
  public class ReportServerProgress : IPackagePostInitializer {
    private readonly IServerClient _serverClient;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IStatusBar _statusBar;

    [ImportingConstructor]
    public ReportServerProgress(
      IServerClient serverClient,
      ISynchronizationContextProvider synchronizationContextProvider,
      IStatusBar statusBar) {
      _serverClient = serverClient;
      _synchronizationContextProvider = synchronizationContextProvider;
      _statusBar = statusBar;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _serverClient.IndexProgress += ServerClient_IndexProgress;
    }

    private void ServerClient_IndexProgress(ProgressParams progress) {
      _synchronizationContextProvider.DispatchThreadContext.Post(() =>
        _statusBar.ReportProgress(progress.DisplayText, progress.Completed, progress.Total));
    }
  }
}
