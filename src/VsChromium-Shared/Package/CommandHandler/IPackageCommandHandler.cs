// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;

namespace VsChromium.Package.CommandHandler {
  /// <summary>
  /// A global command handler.
  /// </summary>
  public interface IPackageCommandHandler {
    CommandID CommandId { get; }
    bool Supported { get; }
    bool Enabled { get; }
    bool Visible { get; }
    bool Checked { get; }
    void Execute(object sender, EventArgs e);
  }
}
