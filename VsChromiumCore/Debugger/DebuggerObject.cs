// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromiumCore.Debugger {
  public class DebuggerObject : IDisposable {
    private DebuggerThread _debuggerThread;

    public void Dispose() {
      if (this._debuggerThread != null) {
        this._debuggerThread.Stop();
        this._debuggerThread = null;
      }
    }

    public void AttachToProcess(int processId) {
      if (this._debuggerThread != null) {
        throw new InvalidOperationException("Debugger already attached to a process.");
      }
      this._debuggerThread = new DebuggerThread(processId);
      this._debuggerThread.Start();
    }
  }
}
