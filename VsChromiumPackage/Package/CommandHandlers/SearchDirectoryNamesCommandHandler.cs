// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromiumPackage.Commands;

namespace VsChromiumPackage.Package.CommandHandlers {
  [Export(typeof(IPackageCommandHandler))]
  public class SearchDirectoryNamesCommandHandler : IPackageCommandHandler {
    private readonly IChromiumExplorerToolWindowAccessor _toolWindowAccessor;

    [ImportingConstructor]
    public SearchDirectoryNamesCommandHandler(IChromiumExplorerToolWindowAccessor toolWindowAccessor) {
      _toolWindowAccessor = toolWindowAccessor;
    }

    public CommandID CommandId { get { return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidSearchDirectoryNames); } }

    public void Execute(object sender, EventArgs e) {
      _toolWindowAccessor.FocusSearchTextBox(CommandId);
    }
  }
}
