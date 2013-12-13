// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromiumPackage.Commands;
using VsChromiumPackage.Package.CommandHandler;

namespace VsChromiumPackage.ToolWindows.ChromiumExplorer {
  [Export(typeof(IPackageCommandHandler))]
  public class SearchFileNamesCommandHandler : IPackageCommandHandler {
    private readonly IChromiumExplorerToolWindowAccessor _toolWindowAccessor;

    [ImportingConstructor]
    public SearchFileNamesCommandHandler(IChromiumExplorerToolWindowAccessor toolWindowAccessor) {
      _toolWindowAccessor = toolWindowAccessor;
    }

    public CommandID CommandId { get { return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidSearchFileNames); } }

    public void Execute(object sender, EventArgs e) {
      _toolWindowAccessor.FocusSearchTextBox(CommandId);
    }
  }
}
