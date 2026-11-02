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
    private readonly Func<bool> _enabled;

    public SimpleCommandTarget(CommandID commandId, Action action) {
      _commandId = commandId;
      _action = action;
      _handlesCommand = () => true;
      _enabled = () => true;
    }

    public SimpleCommandTarget(CommandID commandId, Action action, Func<bool> handlesCommand, Func<bool> enabled) {
      _commandId = commandId;
      _action = action;
      _handlesCommand = handlesCommand;
      _enabled = enabled;
    }

    public bool HandlesCommand(CommandID commandId) {
      return _commandId.Equals(commandId) && _handlesCommand();
    }

    public bool IsEnabled(CommandID commandId) {
      return _enabled();
    }

    public void Execute(CommandID commandId) {
      _action();
    }
  }
}
