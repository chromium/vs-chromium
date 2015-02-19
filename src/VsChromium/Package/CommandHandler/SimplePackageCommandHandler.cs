// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;

namespace VsChromium.Package.CommandHandler {
  public class SimplePackageCommandHandler : PackageCommandHandlerBase {
    private readonly CommandID _commandId;
    private readonly Func<bool> _enabled;
    private readonly EventHandler _execute;

    public SimplePackageCommandHandler(CommandID commandId, Func<bool> enabled, EventHandler execute) {
      _commandId = commandId;
      _enabled = enabled;
      _execute = execute;
    }

    public override CommandID CommandId {
      get { return _commandId; }
    }

    public override bool Supported {
      get { return _enabled(); }
    }

    public override bool Enabled {
      get { return _enabled(); }
    }

    public override void Execute(object sender, EventArgs e) {
      _execute(sender, e);
    }
  }
}