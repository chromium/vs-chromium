// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Windows.Input;
using VsChromium.Core.Logging;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class CommandDelegate : ICommand {
    private readonly Action<object> _action;
    private readonly Func<object, bool> _canExecute;

    public CommandDelegate(Action<object> action)
      : this(action, null) {
    }

    public CommandDelegate(Action<object> action, Func<object, bool> canExecute) {
      _action = action;
      _canExecute = canExecute ?? (x => true);
    }

    public bool CanExecute(object parameter) {
      return _canExecute(parameter);
    }

    public void Execute(object parameter) {
      Logger.WrapActionInvocation(() => _action(parameter));
    }

    public event EventHandler CanExecuteChanged { add { } remove { } }

    public static ICommand Create(Action<object> action) {
      return new CommandDelegate(action);
    }
    public static ICommand Create(Action<object> action, Func<object, bool> canExecute) {
      return new CommandDelegate(action, canExecute);
    }
  }
}