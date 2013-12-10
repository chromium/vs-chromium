// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;

namespace VsChromiumPackage.Package.CommandHandlers {
  /// <summary>
  /// A global command handler.
  /// </summary>
  public interface IPackageCommandHandler {
    CommandID CommandId { get; }
    void Execute(object sender, EventArgs e);
  }
}
