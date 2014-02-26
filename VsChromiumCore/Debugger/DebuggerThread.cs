// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromiumCore.Processes;
using VsChromiumCore.Win32;
using VsChromiumCore.Win32.Debugging;
using VsChromiumCore.Win32.Strings;

namespace VsChromiumCore.Debugger {
  /// <summary>
  /// Runs a debugger thread for a given process id.
  /// </summary>
  public class DebuggerThread {
    private readonly EventWaitHandle _startWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private readonly EventWaitHandle _stopDoneWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private readonly EventWaitHandle _stopWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private Thread _debuggerThread;
    private ProcessResult _processResult;
    private Exception _startError;
    private Exception _stopError;
    private bool _running;

    public ProcessResult Start(Func<ProcessResult> processCreator) {
      new Thread(_ => DebugStart(processCreator)) { IsBackground = true }.Start();

      _startWaitHandle.WaitOne();
      if (_startError != null) {
        throw new Exception("Error starting debugger in separate thread", _startError);
      }
      return _processResult;
    }

    public void Dispose() {
      Stop();
    }

    public void Stop() {
      if (!_running)
        return;

      _stopWaitHandle.Set();
      _stopDoneWaitHandle.WaitOne();
      Debug.Assert(_running == false);
      if (_stopError != null) {
        throw new Exception("Error stopping debugger", _stopError);
      }
    }

    private void DebugStart(Func<ProcessResult> processCreator) {
      try {
        _debuggerThread = Thread.CurrentThread;
        // Note: processCreator is responsible for creating the process in DEBUG mode.
        _processResult = processCreator();
      }
      catch (Exception e) {
        _startError = e;
        return;
      }
      finally {
        _startWaitHandle.Set();
      }

      _running = true;
      try {
        Loop();
      }
      finally {
        _running = false;
      }
    }

    private void DebugStop() {
      try {
        if (Thread.CurrentThread != _debuggerThread) {
          throw new InvalidOperationException("Wrong thread");
        }

        var success = NativeMethods.DebugActiveProcessStop(_processResult.ProcessId);
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
        while(true) {
          var debugEvent = WaitForDebugEvent(10);
          if (debugEvent != null) {
            LogDebugEvent(debugEvent.Value);

            var continueStatus = HandleDebugEvent(debugEvent.Value);
            var success = NativeMethods.ContinueDebugEvent(debugEvent.Value.dwProcessId, debugEvent.Value.dwThreadId,
                                                         continueStatus);
            if (!success)
              throw new LastWin32ErrorException("Error in ContinueDebugEvent");
          }

          // Did we get asked to stop the debugger?
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

    private static CONTINUE_STATUS HandleDebugEvent(DEBUG_EVENT value) {
      if (value.dwDebugEventCode == DEBUG_EVENT_CODE.EXCEPTION_DEBUG_EVENT)
        return CONTINUE_STATUS.DBG_EXCEPTION_NOT_HANDLED;
      else
        return CONTINUE_STATUS.DBG_CONTINUE;
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
      if (!Win32.Processes.NativeMethods.ReadProcessMemory(_processResult.ProcessHandle, debugString.lpDebugStringData, bytes, byteCount, out bytesRead)) {
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
