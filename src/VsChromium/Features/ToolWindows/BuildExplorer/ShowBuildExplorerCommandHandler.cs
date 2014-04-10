// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Commands;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  [Export(typeof(IPackageCommandHandler))]
  public class ShowBuildExplorerCommandHandler
      : ShowToolWindowCommandHandler<BuildExplorerToolWindow> {

    [ImportingConstructor]
    public ShowBuildExplorerCommandHandler(IVisualStudioPackageProvider provider)
      : base(provider, PkgCmdIdList.CmdidBuildExplorerToolWindow) {
    }
  }
}
