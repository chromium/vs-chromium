// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;

namespace VsChromiumPackage.Commands {
  public class SimpleCommandTarget : ICommandTarget {
    private readonly CommandID _commandId;
    private readonly Action _action;

    public SimpleCommandTarget(CommandID commandId, Action action) {
      _commandId = commandId;
      _action = action;
    }

    public bool HandlesCommand(CommandID commandId) {
      return _commandId.Equals(commandId);
    }

    public bool IsEnabled(CommandID commandId) {
      return true;
    }

    public void Execute(CommandID commandId) {
      _action();
    }
  }
}
