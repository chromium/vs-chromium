// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using VsChromium.Core.Debugger;
using VsChromium.Core.Win32;
using VsChromium.Core.Win32.Processes;

namespace VsChromium.Core.Processes {
  [Export(typeof(IProcessCreator))]
  public class ProcessCreator : IProcessCreator {
    public CreateProcessResult CreateProcess(string filename, string arguments, CreateProcessOptions options) {
      Logger.Log("CreateProcess: {0} {1}", filename, arguments);

      // Attaching the debugger ensures the child process will die with the current process.
      var debuggerObject = new DebuggerObject();
      var processInformation = (options & CreateProcessOptions.AttachDebugger) != 0 ?
        debuggerObject.CreateProcess(() => CreateProcessImpl(filename, arguments, options)) :
        CreateProcessImpl(filename, arguments, options);

      Logger.Log("CreateProcess: Creating CreateProcessResult instance.");
      return new CreateProcessResult(processInformation, debuggerObject);
    }

    private ProcessInformation CreateProcessImpl(string filename, string arguments, CreateProcessOptions options) {
      var info = new SimpleProcessStartupInfo {
        FileName = filename,
        Arguments = arguments,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(filename)
      };

      return CreateProcessWithStartInfo(info, options);
    }

    private ProcessInformation CreateProcessWithStartInfo(SimpleProcessStartupInfo simpleProcessStartupInfo, CreateProcessOptions options) {
      Logger.Log("CreateProcessWithStartInfo: Entry point.");
      var stringBuilder = BuildCommandLine(simpleProcessStartupInfo.FileName, simpleProcessStartupInfo.Arguments);
      Logger.Log("CreateProcessWithStartInfo: command line is {0}.", stringBuilder);

      using (var startupInfo = new Win32.Processes.STARTUPINFO()) {
        Logger.Log("CreateProcessWithStartInfo: Creation flags.");
        ProcessCreationFlags processCreationFlags = 0;
        if (simpleProcessStartupInfo.CreateNoWindow) {
          processCreationFlags |= ProcessCreationFlags.CREATE_NO_WINDOW;
        }
        if ((options & CreateProcessOptions.BreakAwayFromJob) != 0) {
          processCreationFlags |= ProcessCreationFlags.CREATE_BREAKAWAY_FROM_JOB;
        }

        var workingDirectory = simpleProcessStartupInfo.WorkingDirectory;
        if (workingDirectory == string.Empty) {
          workingDirectory = Environment.CurrentDirectory;
        }
        Logger.Log("CreateProcessWithStartInfo: Working directory: {0}.", workingDirectory);

        if ((options & CreateProcessOptions.AttachDebugger) != 0) {
          Logger.Log("CreateProcessWithStartInfo: Setting DEBUG_PROCESS flag.");
          processCreationFlags |= ProcessCreationFlags.DEBUG_PROCESS;
          processCreationFlags |= ProcessCreationFlags.DEBUG_ONLY_THIS_PROCESS;
        }

        Logger.Log("CreateProcessWithStartInfo: Calling Win32 CreateProcess.");
        var processInformation = new PROCESS_INFORMATION();
        var environmentPtr = IntPtr.Zero;
        var lastError = 0;
        var success = NativeMethods.CreateProcess(null, stringBuilder, null, null, true, processCreationFlags,
                                                 environmentPtr, workingDirectory, startupInfo, processInformation);
        Logger.Log("CreateProcessWithStartInfo: CreateProcess result: Success={0}-LastError={1}.", success, Marshal.GetLastWin32Error());
        if (!success) {
          lastError = Marshal.GetLastWin32Error();
        }
        // Assign safe handles as quickly as possible to avoid leaks.
        var safeProcessHandle = new SafeProcessHandle(processInformation.hProcess);
        var safeThreadHandle = new SafeProcessHandle(processInformation.hThread);
        if (!success) {
          throw new LastWin32ErrorException(lastError, string.Format("Error creating process from file \"{0}\"", simpleProcessStartupInfo.FileName));
        }

        if (safeProcessHandle.IsInvalid || safeThreadHandle.IsInvalid) {
          Logger.Log("CreateProcessWithStartInfo: Invalid process handle.");
          throw new Exception(string.Format("Error creating process from file \"{0}\" (invalid process handle)", simpleProcessStartupInfo.FileName));
        }

        Logger.Log("CreateProcessWithStartInfo: Creating ProcessResult instance.");
        var processResult = new ProcessInformation {
          ProcessHandle = safeProcessHandle,
          ProcessId = processInformation.dwProcessId
        };
        safeThreadHandle.Close();

        Logger.Log("CreateProcessWithStartInfo: Success!");
        return processResult;
      }
    }

    private static StringBuilder BuildCommandLine(string executableFileName, string arguments) {
      var sb = new StringBuilder();
      executableFileName = executableFileName.Trim();
      bool isQuoted = executableFileName.StartsWith("\"", StringComparison.Ordinal) &&
                      executableFileName.EndsWith("\"", StringComparison.Ordinal);
      if (!isQuoted) {
        sb.Append("\"");
      }
      sb.Append(executableFileName);
      if (!isQuoted) {
        sb.Append("\"");
      }
      if (!string.IsNullOrEmpty(arguments)) {
        sb.Append(" ");
        sb.Append(arguments);
      }
      return sb;
    }

    private class SimpleProcessStartupInfo {
      public string FileName { get; set; }
      public string Arguments { get; set; }
      public bool CreateNoWindow { get; set; }
      public string WorkingDirectory { get; set; }
    }
  }
}
