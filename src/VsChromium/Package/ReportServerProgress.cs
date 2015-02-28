// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.ServerProxy;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Package {
  [Export(typeof(IPackagePostInitializer))]
  public class ReportServerProgress : IPackagePostInitializer {
    private readonly ITypedRequestProcessProxy _typedRequestProcessProxy;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IStatusBar _statusBar;

    [ImportingConstructor]
    public ReportServerProgress(
      ITypedRequestProcessProxy typedRequestProcessProxy,
      ISynchronizationContextProvider synchronizationContextProvider,
      IStatusBar statusBar) {
      _typedRequestProcessProxy = typedRequestProcessProxy;
      _synchronizationContextProvider = synchronizationContextProvider;
      _statusBar = statusBar;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _typedRequestProcessProxy.EventReceived += TypedRequestProcessProxy_EventReceived;
    }

    private void TypedRequestProcessProxy_EventReceived(TypedEvent typedEvent) {
      DispatchProgressReport(typedEvent);
    }

    private void DispatchProgressReport(TypedEvent typedEvent) {
      var progressReportEvent = typedEvent as ProgressReportEvent;
      if (progressReportEvent == null)
        return;

      _synchronizationContextProvider.UIContext.Post(() =>
        _statusBar.ReportProgress(progressReportEvent.DisplayText, progressReportEvent.Completed, progressReportEvent.Total));
    }
  }
}