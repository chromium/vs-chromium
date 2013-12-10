// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using VsChromiumCore.Debugger;
using VsChromiumCore.Win32.Processes;

namespace VsChromiumCore.Processes {
  public interface IProcessCreator {
    ProcessProxy CreateProcess(string filename, string arguments, CreateProcessOptions options);
  }

  [Flags]
  public enum CreateProcessOptions {
    RedirectStdio = 1 << 1,
    AttachDebugger = 1 << 2,
    BreakAwayFromJob = 1 << 3,
  }

  public class ProcessProxy : IDisposable {
    private readonly DebuggerObject _debuggerObject;
    private readonly Process _process;
    private readonly ProcessResult _processResult;

    public ProcessProxy(ProcessResult processResult, DebuggerObject debuggerObject) {
      _process = System.Diagnostics.Process.GetProcessById(processResult.ProcessId);
      _processResult = processResult;
      _debuggerObject = debuggerObject;
    }

    public ProcessResult Process { get { return _processResult; } }

    public void Dispose() {
      if (_debuggerObject != null) {
        _debuggerObject.Dispose();
      }

      if (!_process.HasExited) {
        _process.Kill();
        _process.WaitForExit();
      }
    }
  }

  public class ProcessResult {
    public StreamWriter StandardInput { get; set; }
    public StreamReader StandardOutput { get; set; }
    public StreamReader StandardError { get; set; }
    internal SafeProcessHandle ProcessHandle { get; set; }
    public int ProcessId { get; set; }

    public IntPtr Handle { get { return ProcessHandle.DangerousGetHandle(); } }
  }
}
