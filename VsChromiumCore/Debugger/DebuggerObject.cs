// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromiumCore.Debugger {
  public class DebuggerObject : IDisposable {
    private DebuggerThread _debuggerThread;

    public void Dispose() {
      if (_debuggerThread != null) {
        _debuggerThread.Stop();
        _debuggerThread = null;
      }
    }

    public void AttachToProcess(int processId) {
      if (_debuggerThread != null) {
        throw new InvalidOperationException("Debugger already attached to a process.");
      }
      _debuggerThread = new DebuggerThread(processId);
      _debuggerThread.Start();
    }
  }
}
