// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Web.WebView2.Core;
using VsChromium.Commands;
using VsChromium.Core.Logging;
using VsChromium.Features.ToolWindows;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>Shows the "VsChromium Server" tool window.</summary>
  public interface IServerDashboardService {
    /// <summary>
    /// Shows the server dashboard. Returns false if it can't be shown (no
    /// WebView2 runtime): the caller shows the legacy status dialog instead.
    /// </summary>
    bool ShowDashboard();
  }

  [Export(typeof(IServerDashboardService))]
  public class ServerDashboardService : IServerDashboardService {
    private readonly IVisualStudioPackageProvider _packageProvider;

    [ImportingConstructor]
    public ServerDashboardService(IVisualStudioPackageProvider packageProvider) {
      _packageProvider = packageProvider;
    }

    public bool ShowDashboard() {
      ThreadHelper.ThrowIfNotOnUIThread();
      try {
        CheckWebView2Runtime();
      }
      catch (Exception e) {
        // Note: Also the errors loading the WebView2 assemblies of Visual Studio
        // (older than the referenced version).
        Logger.LogWarn(e, "WebView2 not available: the server dashboard can't be shown.");
        return false;
      }
      var window = _packageProvider.Package.FindToolWindow(typeof(ServerDashboardToolWindow), 0, true);
      if (window?.Frame == null) {
        return false;
      }
      ErrorHandler.ThrowOnFailure(((IVsWindowFrame)window.Frame).Show());
      return true;
    }

    /// <summary>
    /// Throws if the WebView2 runtime is missing. In its own method (not
    /// inlined): the WebView2 assemblies are loaded when it is compiled.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CheckWebView2Runtime() {
      CoreWebView2Environment.GetAvailableBrowserVersionString();
    }
  }

  /// <summary>"View > Other Windows > VsChromium Server".</summary>
  [Export(typeof(IPackageCommandHandler))]
  public class ShowServerDashboardCommandHandler : ShowToolWindowCommandHandler<ServerDashboardToolWindow> {
    [ImportingConstructor]
    public ShowServerDashboardCommandHandler(IVisualStudioPackageProvider provider)
      : base(provider, (int)PkgCmdIdList.CmdidServerDashboardToolWindow) {
    }
  }
}
