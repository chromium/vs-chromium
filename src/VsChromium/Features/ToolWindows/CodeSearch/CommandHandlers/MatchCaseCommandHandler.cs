// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;
using VsChromium.Commands;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.CodeSearch.CommandHandlers {
  public class MatchCaseCommandHandler : PackageCommandHandlerBase {
    private readonly CodeSearchToolWindow _window;

    public MatchCaseCommandHandler(CodeSearchToolWindow window) {
      _window = window;
    }

    public override CommandID CommandId {
      get {
        return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidMatchCase);
      }
    }

    public override void Execute(object sender, EventArgs e) {
      _window.ExplorerControl.ViewModel.MatchCase =
        !_window.ExplorerControl.ViewModel.MatchCase;
    }
  }
}