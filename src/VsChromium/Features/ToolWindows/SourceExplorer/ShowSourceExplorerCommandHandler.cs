// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Commands;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  [Export(typeof(IPackageCommandHandler))]
  public class ShowSourceExplorerCommandHandler 
      : ShowToolWindowCommandHandler<SourceExplorerToolWindow> {

    [ImportingConstructor]
    public ShowSourceExplorerCommandHandler(IVisualStudioPackageProvider provider)
      : base(provider, PkgCmdIdList.CmdidSourceExplorerToolWindow) {
    }
  }
}
