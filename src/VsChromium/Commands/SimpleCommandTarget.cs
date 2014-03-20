// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;

namespace VsChromium.Commands {
  public class SimpleCommandTarget : ICommandTarget {
    private readonly CommandID _commandId;
    private readonly Action _action;
    private readonly Func<bool> _handlesCommand;

    public SimpleCommandTarget(CommandID commandId, Action action) {
      _commandId = commandId;
      _action = action;
    }

    public SimpleCommandTarget(CommandID commandId, Action action, Func<bool> handlesCommand) {
      _commandId = commandId;
      _action = action;
      _handlesCommand = handlesCommand;
    }

    public bool HandlesCommand(CommandID commandId) {
      bool result = _commandId.Equals(commandId);
      if (result && _handlesCommand != null)
        result = _handlesCommand();
      return result;
    }

    public bool IsEnabled(CommandID commandId) {
      return true;
    }

    public void Execute(CommandID commandId) {
      _action();
    }
  }
}
