using System;
using System.Diagnostics;
using System.IO;
using VsChromiumCore.Debugger;
using VsChromiumCore.Win32.Processes;

namespace VsChromiumCore.Processes {
  public class CreateProcessResult : IDisposable {
    private readonly SafeProcessHandle _processHandle;
    private readonly DebuggerObject _debuggerObject;
    private readonly StreamWriter _standardInput;
    private readonly StreamReader _standardOutput;
    private readonly StreamReader _standardError;
    private readonly Process _managedProcess;

    public CreateProcessResult(SafeProcessHandle processHandle, int processId, DebuggerObject debuggerObject,
      StreamWriter standardInput, StreamReader standardOutput, StreamReader standardError) {
      _managedProcess = Process.GetProcessById(processId);
      _processHandle = processHandle;
      _debuggerObject = debuggerObject;
      _standardInput = standardInput;
      _standardOutput = standardOutput;
      _standardError = standardError;
    }

    public Process Process { get { return _managedProcess; } }
    public StreamWriter StandardInput { get { return _standardInput; } }
    public StreamReader StandardOutput { get { return _standardOutput; } }
    public StreamReader StandardError { get { return _standardError; } }

    public void Dispose() {
      _debuggerObject.Dispose();
      if (!_managedProcess.HasExited) {
        _managedProcess.Kill();
        _managedProcess.WaitForExit();
      }
      _processHandle.Dispose();
    }
  }
}