// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;

namespace VsChromium.Package.CommandHandler {
  public abstract class PackageCommandHandlerBase : IPackageCommandHandler {
    public abstract CommandID CommandId { get; }
    public virtual bool Supported { get { return true; } }
    public virtual bool Enabled { get { return true; } }
    public virtual bool Visible { get { return true; } }
    public virtual bool Checked { get { return false; } }
    public abstract void Execute(object sender, EventArgs e);
  }
}