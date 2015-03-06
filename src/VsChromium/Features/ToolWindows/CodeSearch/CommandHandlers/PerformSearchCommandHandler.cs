// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;
using VsChromium.Commands;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.CodeSearch.CommandHandlers {
  public class PerformSearchCommandHandler : PackageCommandHandlerBase {
    private readonly CodeSearchToolWindow _window;

    public PerformSearchCommandHandler(CodeSearchToolWindow window) {
      _window = window;
    }

    public override CommandID CommandId {
      get {
        return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidPerformSearch);
      }
    }

    public override bool Enabled {
      get { return _window.IsPerformSearchEnabled; }
    }

    public override void Execute(object sender, EventArgs e) {
      _window.PerformSearch();
    }
  }
}