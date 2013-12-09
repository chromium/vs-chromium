// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VsChromiumCore.Debugger;
using VsChromiumCore.Win32.Interop;
using VsChromiumCore.Win32.Processes;

namespace VsChromiumCore.Processes {
  [Export(typeof(IProcessCreator))]
  public class ProcessCreator : IProcessCreator {
    public ProcessProxy CreateProcess(string filename, string arguments, CreateProcessOptions options) {
      // Start process
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
      var processResult = StartWithCreateProcess(info, options);

      // Attaching the debugger ensures the child process will die with the current process.
      DebuggerObject debuggerObject = null;
      if ((options & CreateProcessOptions.AttachDebugger) != 0) {
        debuggerObject = new DebuggerObject();
        debuggerObject.AttachToProcess(processResult.ProcessId);
      }

      return new ProcessProxy(processResult, debuggerObject);
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
        throw new Win32Exception();
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
        if (
            !Win32.Handles.NativeMethods.DuplicateHandle(new HandleRef(this, NativeMethods.GetCurrentProcess()),
                safeFileHandle, new HandleRef(this, NativeMethods.GetCurrentProcess()), out parentHandle, 0, false, 2)) {
          throw new Win32Exception();
        }
      }
      finally {
        if (safeFileHandle != null && !safeFileHandle.IsInvalid) {
          safeFileHandle.Close();
        }
      }
    }

    private ProcessResult StartWithCreateProcess(ProcessStartInfo startInfo, CreateProcessOptions options) {
      var stringBuilder = BuildCommandLine(startInfo.FileName, startInfo.Arguments);
      var startupInfo = new Startupinfo();
      var processInformation = new ProcessInformation();
      var safeProcessHandle = new SafeProcessHandle();
      var safeThreadHandle = new SafeThreadHandle();
      SafeFileHandle stdin = null;
      SafeFileHandle stdout = null;
      SafeFileHandle stderr = null;
      try {
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
        ProcessCreationFlags processCreationFlags = 0;
        if (startInfo.CreateNoWindow) {
          processCreationFlags |= ProcessCreationFlags.CREATE_NO_WINDOW;
        }
        var text = startInfo.WorkingDirectory;
        if (text == string.Empty) {
          text = Environment.CurrentDirectory;
        }

        if ((options & CreateProcessOptions.BreakAwayFromJob) != 0) {
          processCreationFlags |= ProcessCreationFlags.CREATE_BREAKAWAY_FROM_JOB;
        }
        var environmentPtr = IntPtr.Zero;
        var lastError = 0;
        var result = NativeMethods.CreateProcess(null, stringBuilder, null, null, true, processCreationFlags,
            environmentPtr, text, startupInfo, processInformation);
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
          throw new Win32Exception(lastError);
        }
      }
      finally {
        startupInfo.Dispose();
      }

      var processResult = new ProcessResult();

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

      bool success = false;
      if (!safeProcessHandle.IsInvalid) {
        processResult.ProcessHandle = safeProcessHandle;
        processResult.ProcessId = processInformation.dwProcessId;
        safeThreadHandle.Close();
        success = true;
      }

      if (!success)
        return null;
      return processResult;
    }
  }
}
