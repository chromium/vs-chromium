// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Windows.Input;
using VsChromium.Core;
using VsChromium.Core.Logging;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class CommandDelegate : ICommand {
    private readonly Action<object> _action;

    public CommandDelegate(Action<object> action) {
      _action = action;
    }

    public bool CanExecute(object parameter) {
      return true;
    }

    public void Execute(object parameter) {
      Logger.WrapActionInvocation(() =>_action(parameter));
    }

    public event EventHandler CanExecuteChanged { add { } remove{} }

    public static ICommand Create(Action<object> action) {
      return new CommandDelegate(action);
    }
  }
}