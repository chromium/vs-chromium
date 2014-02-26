// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VsChromiumCore.Debugger;
using VsChromiumCore.Win32;
using VsChromiumCore.Win32.Interop;
using VsChromiumCore.Win32.Processes;

namespace VsChromiumCore.Processes {
  [Export(typeof(IProcessCreator))]
  public class ProcessCreator : IProcessCreator {
    public CreateProcessResult CreateProcess(string filename, string arguments, CreateProcessOptions options) {
      Logger.Log("CreateProcess: {0} {1}", filename, arguments);

      // Attaching the debugger ensures the child process will die with the current process.
      var debuggerObject = new DebuggerObject();
      var processResult = (options & CreateProcessOptions.AttachDebugger) != 0 ?
        debuggerObject.CreateProcess(() => CreateProcessImpl(filename, arguments, options)) :
        CreateProcessImpl(filename, arguments, options);

      Logger.Log("CreateProcess: Creating CreateProcessResult instance.");
      return new CreateProcessResult(processResult.ProcessHandle, processResult.ProcessId, debuggerObject,
        processResult.StandardInput, processResult.StandardOutput, processResult.StandardError);
    }

    private ProcessResult CreateProcessImpl(string filename, string arguments, CreateProcessOptions options) {
      var info = new ProcessStartInfo();
      info.Arguments = arguments;
      info.CreateNoWindow = true;
      info.ErrorDialog = false;
      info.FileName = filename;
      info.LoadUserProfile = true;
      info.WorkingDirectory = Path.GetDirectoryName(info.FileName);
      info.UseShellExecute = false;
      if ((options & CreateProcessOptions.RedirectStdio) != 0) {
        info.RedirectStandardError = true;
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
      }

      return CreateProcessWithStartInfo(info, options);
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

    private static void CreatePipeWithSecurityAttributes(
      out SafeFileHandle hReadPipe,
      out SafeFileHandle hWritePipe,
      SecurityAttributes lpPipeAttributes,
      int nSize) {
      bool flag = Win32.NamedPipes.NativeMethods.CreatePipe(out hReadPipe, out hWritePipe, lpPipeAttributes, nSize);
      if (!flag || hReadPipe.IsInvalid || hWritePipe.IsInvalid) {
        throw new LastWin32ErrorException("Error calling CreatePipe");
      }
    }

    private void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs) {
      var securityAttributes = new SecurityAttributes();
      securityAttributes.bInheritHandle = true;
      SafeFileHandle safeFileHandle = null;
      try {
        if (parentInputs) {
          CreatePipeWithSecurityAttributes(out childHandle, out safeFileHandle, securityAttributes, 0);
        } else {
          CreatePipeWithSecurityAttributes(out safeFileHandle, out childHandle, securityAttributes, 0);
        }
        if (!Win32.Handles.NativeMethods.DuplicateHandle(new HandleRef(this, NativeMethods.GetCurrentProcess()),
                                                       safeFileHandle, new HandleRef(this, NativeMethods.GetCurrentProcess()), out parentHandle, 0, false, 2)) {
          throw new LastWin32ErrorException("Error calling DuplicateHandle.");
        }
      }
      finally {
        if (safeFileHandle != null && !safeFileHandle.IsInvalid) {
          safeFileHandle.Close();
        }
      }
    }

    private ProcessResult CreateProcessWithStartInfo(ProcessStartInfo startInfo, CreateProcessOptions options) {
      Logger.Log("CreateProcessWithStartInfo: Entry point.");
      var stringBuilder = BuildCommandLine(startInfo.FileName, startInfo.Arguments);
      Logger.Log("CreateProcessWithStartInfo: command line is {0}.", stringBuilder);

      var startupInfo = new Startupinfo();
      var processInformation = new ProcessInformation();
      var safeProcessHandle = new SafeProcessHandle();
      var safeThreadHandle = new SafeThreadHandle();
      SafeFileHandle stdin = null;
      SafeFileHandle stdout = null;
      SafeFileHandle stderr = null;
      try {
        Logger.Log("CreateProcessWithStartInfo: Creating named pipe handles.");
        if (startInfo.RedirectStandardInput || startInfo.RedirectStandardOutput || startInfo.RedirectStandardError) {
          if (startInfo.RedirectStandardInput) {
            CreatePipe(out stdin, out startupInfo.hStdInput, true);
          } else {
            startupInfo.hStdInput = new SafeFileHandle(Win32.Handles.NativeMethods.GetStdHandle(-10), false);
          }
          if (startInfo.RedirectStandardOutput) {
            CreatePipe(out stdout, out startupInfo.hStdOutput, false);
          } else {
            startupInfo.hStdOutput = new SafeFileHandle(Win32.Handles.NativeMethods.GetStdHandle(-11), false);
          }
          if (startInfo.RedirectStandardError) {
            CreatePipe(out stderr, out startupInfo.hStdError, false);
          } else {
            startupInfo.hStdError = new SafeFileHandle(Win32.Handles.NativeMethods.GetStdHandle(-12), false);
          }
          startupInfo.dwFlags = 256;
        }
        Logger.Log("CreateProcessWithStartInfo: Creation flags.");
        ProcessCreationFlags processCreationFlags = 0;
        if (startInfo.CreateNoWindow) {
          processCreationFlags |= ProcessCreationFlags.CREATE_NO_WINDOW;
        }
        if ((options & CreateProcessOptions.BreakAwayFromJob) != 0) {
          processCreationFlags |= ProcessCreationFlags.CREATE_BREAKAWAY_FROM_JOB;
        }

        var workingDirectory = startInfo.WorkingDirectory;
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
        var environmentPtr = IntPtr.Zero;
        var lastError = 0;
        var result = NativeMethods.CreateProcess(null, stringBuilder, null, null, true, processCreationFlags,
                                                 environmentPtr, workingDirectory, startupInfo, processInformation);
        Logger.Log("CreateProcessWithStartInfo: CreateProcess result: Success={0}-LastError={1}.", result, Marshal.GetLastWin32Error());
        if (!result) {
          lastError = Marshal.GetLastWin32Error();
        }
        if (processInformation.hProcess != IntPtr.Zero &&
            processInformation.hProcess != Win32.Handles.NativeMethods.INVALID_HANDLE_VALUE) {
          safeProcessHandle.InitialSetHandle(processInformation.hProcess);
        }
        if (processInformation.hThread != IntPtr.Zero &&
            processInformation.hThread != Win32.Handles.NativeMethods.INVALID_HANDLE_VALUE) {
          safeThreadHandle.InitialSetHandle(processInformation.hThread);
        }
        if (!result) {
          throw new LastWin32ErrorException(lastError, string.Format("Error creating process from file \"{0}\"", startInfo.FileName));
        }
      }
      finally {
        Logger.Log("CreateProcessWithStartInfo: Disposing of startupInfo.");
        startupInfo.Dispose();
      }

      Logger.Log("CreateProcessWithStartInfo: Creating ProcessResult instance.");
      var processResult = new ProcessResult();

      Logger.Log("CreateProcessWithStartInfo: Creating streams for std handles.");

      if (startInfo.RedirectStandardInput) {
        processResult.StandardInput = new StreamWriter(new FileStream(stdin, FileAccess.Write, 4096, false),
                                                       Console.InputEncoding, 4096);
        processResult.StandardInput.AutoFlush = true;
      }

      if (startInfo.RedirectStandardOutput) {
        Encoding encoding = (startInfo.StandardOutputEncoding != null)
                              ? startInfo.StandardOutputEncoding
                              : Console.OutputEncoding;
        processResult.StandardOutput = new StreamReader(new FileStream(stdout, FileAccess.Read, 4096, false), encoding,
                                                        true, 4096);
      }

      if (startInfo.RedirectStandardError) {
        Encoding encoding = (startInfo.StandardErrorEncoding != null)
                              ? startInfo.StandardErrorEncoding
                              : Console.OutputEncoding;
        processResult.StandardError = new StreamReader(new FileStream(stderr, FileAccess.Read, 4096, false), encoding,
                                                       true, 4096);
      }

      if (safeProcessHandle.IsInvalid) {
        Logger.Log("CreateProcessWithStartInfo: Invalid process handle.");
        return null;
      }

      Logger.Log("CreateProcessWithStartInfo: Saving process handle.");
      processResult.ProcessHandle = safeProcessHandle;
      processResult.ProcessId = processInformation.dwProcessId;
      safeThreadHandle.Close();

      Logger.Log("CreateProcessWithStartInfo: Success!");
      return processResult;
    }
  }
}
