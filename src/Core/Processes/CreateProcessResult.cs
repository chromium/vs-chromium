// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using VsChromium.Core.Debugger;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Processes {
  public class CreateProcessResult : IDisposable {
    private readonly ProcessInformation _processInformation;
    private readonly DebuggerObject _debuggerObject;
    private readonly Process _process;

    public CreateProcessResult(ProcessInformation processInformation, DebuggerObject debuggerObject) {
      _processInformation = processInformation;
      _debuggerObject = debuggerObject;
      _process = Process.GetProcessById(processInformation.ProcessId);
    }

    public Process Process { get { return _process; } }

    public void Dispose() {
      _debuggerObject.Dispose();
      KillProcess();
      _processInformation.Dispose();
    }

    private void KillProcess() {
      try {
        if (!_process.HasExited) {
          _process.Kill();
          _process.WaitForExit(1000); // Don't wait infinitely.
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Error killing process.");
      }
    }
  }
}