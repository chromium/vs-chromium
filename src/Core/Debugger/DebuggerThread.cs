// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromium.Core.Processes;
using VsChromium.Core.Win32;
using VsChromium.Core.Win32.Debugging;
using VsChromium.Core.Win32.Strings;

namespace VsChromium.Core.Debugger {
  /// <summary>
  /// Runs a debugger thread for a given process id.
  /// </summary>
  public class DebuggerThread {
    private readonly EventWaitHandle _stopDoneWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private readonly EventWaitHandle _stopWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private Thread _debuggerThread;
    private ProcessInformation _processInformation;
    private int _processId;
    private Exception _stopError;
    private bool _running;

    public ProcessInformation Start(Func<ProcessInformation> processCreator) {
      var startWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
      ProcessInformation result = null;
      Exception startError = null;
      ParameterizedThreadStart parameterizedThreadStart = _ => DebuggerStart(startWaitHandle, processCreator, pi => result = pi, e => startError = e);
      new Thread(parameterizedThreadStart) { IsBackground = true }.Start();
      startWaitHandle.WaitOne();
      if (startError != null) {
        throw new Exception("Error starting debugger thread", startError);
      }
      return result;
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

    private void DebuggerStart(EventWaitHandle startWaitHandle, Func<ProcessInformation> processCreator, Action<ProcessInformation> callback, Action<Exception> errorCallback) {
      try {
        _debuggerThread = Thread.CurrentThread;
        // Note: processCreator is responsible for creating the process in DEBUG mode.
        // Note: If processCreator throws an exception after creating the
        // process, we may end up in a state where we received debugging events
        // without having "_processInformation" set. We need to be able to deal
        // with that.
        _processInformation = processCreator();
        callback(_processInformation);
      }
      catch (Exception e) {
        errorCallback(e);
        return;
      }
      finally {
        startWaitHandle.Set();
      }

      _running = true;
      try {
        DebuggerLoop();
      }
      finally {
        _running = false;
      }
    }

    private void DebuggerStop() {
      try {
        if (Thread.CurrentThread != _debuggerThread) {
          throw new InvalidOperationException("Wrong thread");
        }

        if (_processId != 0) {
          var success = NativeMethods.DebugActiveProcessStop(_processId);
          if (!success)
            throw new LastWin32ErrorException("Error in DebugActiveProcessStop");
        }
      }
      catch (Exception e) {
        _stopError = e;
      }
      finally {
        _stopDoneWaitHandle.Set();
      }
    }

    private void DebuggerLoop() {
      if (Thread.CurrentThread != _debuggerThread) {
        throw new InvalidOperationException("Wrong thread");
      }

      try {
        while(true) {
          var debugEvent = WaitForDebugEvent(10);
          if (debugEvent != null) {
            Debug.Assert(_processId == 0 || _processId == debugEvent.Value.dwProcessId);
            _processId = debugEvent.Value.dwProcessId;
            LogDebugEvent(debugEvent.Value);

            var continueStatus = HandleDebugEvent(debugEvent.Value);
            var success = NativeMethods.ContinueDebugEvent(debugEvent.Value.dwProcessId, debugEvent.Value.dwThreadId,
                                                         continueStatus);
            if (!success)
              throw new LastWin32ErrorException("Error in ContinueDebugEvent");
          }

          // Did we get asked to stop the debugger?
          if (_stopWaitHandle.WaitOne(1)) {
            DebuggerStop();
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
      if (_processInformation == null)
        return "<no process handle>";

      // Note: Sometimes when debugging unit tests, we get AccessViolationException reading bytes from the debuggee.
#if false
      return "<no processed>";
#else
      var isUnicode = (debugString.fUnicode != 0);
      var byteCount = (uint)(debugString.nDebugStringLength * (isUnicode ? sizeof(char) : sizeof(byte)));
      var bytes = new byte[byteCount];
      uint bytesRead;
      if (!Win32.Processes.NativeMethods.ReadProcessMemory(_processInformation.ProcessHandle, debugString.lpDebugStringData, bytes, byteCount, out bytesRead)) {
        return string.Format("<error getting debug string from process: {0}>", new LastWin32ErrorException().Message);
      }

      var message = (isUnicode ? Conversion.UnicodeToUnicode(bytes) : Conversion.AnsiToUnicode(bytes));
      message = message.TrimEnd('\r', '\n');
      return message;
#endif
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
