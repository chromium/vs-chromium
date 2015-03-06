// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.CodeSearch.CommandHandlers {
  public class PreviousLocationCommandHandler : PackageCommandHandlerBase {
    private readonly CodeSearchToolWindow _window;

    public PreviousLocationCommandHandler(CodeSearchToolWindow window) {
      _window = window;
    }

    public override CommandID CommandId {
      get {
        return new CommandID(
          VSConstants.GUID_VSStandardCommandSet97,
          (int)VSConstants.VSStd97CmdID.PreviousLocation);
      }
    }

    public override bool Enabled {
      get { return _window.HasPreviousLocation(); }
    }

    public override void Execute(object sender, EventArgs e) {
      _window.NavigateToPreviousLocation();
    }
  }
}