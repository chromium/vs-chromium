// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;
using VsChromium.Commands;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class CancelSearchCommandHandler : PackageCommandHandlerBase {
    private readonly SourceExplorerToolWindow _window;

    public CancelSearchCommandHandler(SourceExplorerToolWindow window) {
      _window = window;
    }

    public override CommandID CommandId {
      get {
        return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidCancelSearch);
      }
    }

    public override bool Supported {
      get { return this.Enabled; }
    }

    public override bool Enabled {
      get { return _window.IsCancelSearchEnabled; }
    }

    public override void Execute(object sender, EventArgs e) {
      _window.CancelSearch();
    }
  }
}