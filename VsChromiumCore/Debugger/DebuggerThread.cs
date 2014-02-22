// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromiumCore.Win32;
using VsChromiumCore.Win32.Debugging;
using VsChromiumCore.Win32.Strings;

namespace VsChromiumCore.Debugger {
  /// <summary>
  /// Runs a debugger thread for a given process id.
  /// </summary>
  public class DebuggerThread {
    private readonly int _processId;

    private readonly EventWaitHandle _startWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private readonly EventWaitHandle _stopDoneWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private readonly EventWaitHandle _stopWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private Thread _debuggerThread;
    private Exception _startError;
    private Exception _stopError;

    public DebuggerThread(int processId) {
      _processId = processId;
    }

    public void Start() {
      new Thread(DebugStart) { IsBackground = true }.Start();

      _startWaitHandle.WaitOne();
      if (_startError != null) {
        throw new Exception("Error starting debugger in separate thread", _startError);
      }
    }

    public void Stop() {
      _stopWaitHandle.Set();
      _stopDoneWaitHandle.WaitOne();
      if (_stopError != null) {
        throw new Exception("Error stopping debugger", _stopError);
      }
    }

    private void DebugStart() {
      try {
        _debuggerThread = Thread.CurrentThread;
        var result = NativeMethods.DebugActiveProcess(_processId);
        if (!result) {
          var hr = Marshal.GetHRForLastWin32Error();
          if (hr == HResults.HR_ERROR_NOT_SUPPORTED) {
            throw new InvalidOperationException("Trying to attach to a x64 process from a x86 process.");
          }
          throw new LastWin32ErrorException("Error in DebugActiveProcess");
        }
      }
      catch (Exception e) {
        _startError = e;
      }
      finally {
        _startWaitHandle.Set();
      }

      if (_startError == null) {
        Loop();
      }
    }

    private void DebugStop() {
      try {
        if (Thread.CurrentThread != _debuggerThread) {
          throw new InvalidOperationException("Wrong thread");
        }

        var success = NativeMethods.DebugActiveProcessStop(_processId);
        if (!success)
          throw new LastWin32ErrorException("Error in DebugActiveProcessStop");
      }
      catch (Exception e) {
        _stopError = e;
      }
      finally {
        _stopDoneWaitHandle.Set();
      }
    }

    private void Loop() {
      if (Thread.CurrentThread != _debuggerThread) {
        throw new InvalidOperationException("Wrong thread");
      }

      try {
        for (; ; ) {
          var debugEvent = WaitForDebugEvent(10);
          if (debugEvent != null) {
            LogDebugEvent(debugEvent.Value);

            const CONTINUE_STATUS continueStatus = CONTINUE_STATUS.DBG_CONTINUE;
            var success = NativeMethods.ContinueDebugEvent(debugEvent.Value.dwProcessId, debugEvent.Value.dwThreadId,
                                                         continueStatus);
            if (!success)
              throw new LastWin32ErrorException("Error in ContinueDebugEvent");
          }

          // Did we get ask to stop the debugger?
          if (_stopWaitHandle.WaitOne(1)) {
            DebugStop();
            break;
          }
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Exception in debugger loop");
      }
    }

    private void LogDebugEvent(DEBUG_EVENT debugEvent) {
      switch (debugEvent.dwDebugEventCode) {
        case DEBUG_EVENT_CODE.OUTPUT_DEBUG_STRING_EVENT:
          Logger.Log("{0}", GetOutputDebugString(debugEvent.DebugString));
          break;
        case DEBUG_EVENT_CODE.CREATE_THREAD_DEBUG_EVENT:
        case DEBUG_EVENT_CODE.EXIT_THREAD_DEBUG_EVENT:
        case DEBUG_EVENT_CODE.LOAD_DLL_DEBUG_EVENT:
        case DEBUG_EVENT_CODE.UNLOAD_DLL_DEBUG_EVENT:
          // Too noisy...
          break;
        default:
          Logger.Log("DBGEVENT: {0}: {1}", debugEvent.dwDebugEventCode, GetDebugEventText(debugEvent));
          break;
      }
    }

    private string GetDebugEventText(DEBUG_EVENT debugEvent) {
      switch (debugEvent.dwDebugEventCode) {
        case DEBUG_EVENT_CODE.EXIT_THREAD_DEBUG_EVENT:
          return string.Format("Thread Exit Code: 0x{0:x8}", debugEvent.ExitThread.dwExitCode);

        case DEBUG_EVENT_CODE.EXIT_PROCESS_DEBUG_EVENT:
          return string.Format("Process Exit Code: 0x{0:x8}", debugEvent.ExitProcess.dwExitCode);

        case DEBUG_EVENT_CODE.OUTPUT_DEBUG_STRING_EVENT:
          return GetOutputDebugString(debugEvent.DebugString);

        case DEBUG_EVENT_CODE.EXCEPTION_DEBUG_EVENT:
          var exceptionEvent = debugEvent.Exception;
          return string.Format("Code: 0x{0:x8}, Flags: 0x{1:x8}, FirstChance: {2}", exceptionEvent.ExceptionRecord.ExceptionCode, exceptionEvent.ExceptionRecord.ExceptionFlags, exceptionEvent.dwFirstChance);

        default:
          return "";
      }
    }

    private string GetOutputDebugString(OUTPUT_DEBUG_STRING_INFO debugString) {
      var isUnicode = (debugString.fUnicode != 0);
      var byteCount = (uint)(debugString.nDebugStringLength * (isUnicode ? sizeof(char) : sizeof(byte)));
      var bytes = new byte[byteCount];
      uint bytesRead;
      var hProcess = Process.GetProcessById(this._processId).Handle;
      if (!Win32.Processes.NativeMethods.ReadProcessMemory(hProcess, debugString.lpDebugStringData, bytes, byteCount, out bytesRead)) {
        return string.Format("<error getting debug string from process: {0}>", new LastWin32ErrorException().Message);
      }

      var message = (isUnicode ? Conversion.UnicodeToUnicode(bytes) : Conversion.AnsiToUnicode(bytes));
      message = message.TrimEnd('\r', '\n');
      return message;
    }

    private static DEBUG_EVENT? WaitForDebugEvent(uint timeout) {
      DEBUG_EVENT debugEvent;
      var success = NativeMethods.WaitForDebugEvent(out debugEvent, timeout);
      if (!success) {
        int hr = Marshal.GetHRForLastWin32Error();
        if (hr == HResults.HR_ERROR_SEM_TIMEOUT)
          return null;

        Marshal.ThrowExceptionForHR(hr);
      }
      return debugEvent;
    }
  }
}
