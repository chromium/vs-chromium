// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using VsChromium.Commands;

namespace VsChromium.Features.ServerDashboard {
  /// <summary>
  /// The "VsChromium Server" tool window: the server dashboard (the same page
  /// as the VS Code extension, in WebView2).
  /// </summary>
  [Guid(GuidList.GuidServerDashboardToolWindowString)]
  public class ServerDashboardToolWindow : ToolWindowPane {
    public ServerDashboardToolWindow()
      : base(null) {
      Caption = "VsChromium Server";
      BitmapResourceID = 301;
      BitmapIndex = 1;
      // Note: ToolWindowPane disposes the content.
      Content = new ServerDashboardControl();
    }

    public ServerDashboardControl DashboardControl => (ServerDashboardControl)Content;

    public override void OnToolWindowCreated() {
      base.OnToolWindowCreated();
      DashboardControl.OnToolWindowCreated(this);
    }
  }
}
