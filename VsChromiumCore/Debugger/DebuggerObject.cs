// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.Processes;

namespace VsChromiumCore.Debugger {
  /// <summary>
  /// Proxy object used to control a debugger thread. The debugger thread is
  /// started only when a process is attached.
  /// </summary>
  public class DebuggerObject : IDisposable {
    private DebuggerThread _debuggerThread;

    public ProcessInformation CreateProcess(Func<ProcessInformation> processCreator) {
      if (_debuggerThread != null) {
        throw new InvalidOperationException("Debugger already attached to a process.");
      }
      _debuggerThread = new DebuggerThread();
      return _debuggerThread.Start(processCreator);
    }

    public void Dispose() {
      if (_debuggerThread != null) {
        _debuggerThread.Dispose();
        _debuggerThread = null;
      }
    }
  }
}
