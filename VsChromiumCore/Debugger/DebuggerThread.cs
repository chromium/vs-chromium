// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VsChromiumCore.Debugger {
  public class DebuggerThread {
    private readonly int _processId;

    private readonly EventWaitHandle _startWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private readonly EventWaitHandle _stopDoneWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private readonly EventWaitHandle _stopWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    private Thread _debuggerThread;
    private Exception _startError;
    private Exception _stopError;

    public DebuggerThread(int processId) {
      this._processId = processId;
    }

    public void Start() {
      new Thread(DebugStart) { IsBackground = true }.Start();

      this._startWaitHandle.WaitOne();
      if (this._startError != null) {
        throw new Exception("Error starting debugger in separate thread", this._startError);
      }
    }

    public void Stop() {
      this._stopWaitHandle.Set();
      this._stopDoneWaitHandle.WaitOne();
      if (this._stopError != null) {
        throw new Exception("Error stopping debugger", this._stopError);
      }
    }

    private void DebugStart() {
      try {
        this._debuggerThread = Thread.CurrentThread;
        var result = DebuggerApi.DebugActiveProcess(this._processId);
        if (!result) {
          var hr = Marshal.GetHRForLastWin32Error();
          if (hr == Win32.HR_ERROR_NOT_SUPPORTED) {
            throw new InvalidOperationException("Trying to attach to a x64 process from a x86 process.");
          }
          Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }
      }
      catch (Exception e) {
        this._startError = e;
      }
      finally {
        this._startWaitHandle.Set();
      }

      if (this._startError == null) {
        Loop();
      }
    }

    private void DebugStop() {
      try {
        if (Thread.CurrentThread != this._debuggerThread) {
          throw new InvalidOperationException("Wrong thread");
        }

        var result = DebuggerApi.DebugActiveProcessStop(this._processId);
        if (!result)
          Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
      }
      catch (Exception e) {
        this._stopError = e;
      }
      finally {
        this._stopDoneWaitHandle.Set();
      }
    }

    private void Loop() {
      if (Thread.CurrentThread != this._debuggerThread) {
        throw new InvalidOperationException("Wrong thread");
      }

      try {
        for (;;) {
          var debugEvent = WaitForDebugEvent(10);
          if (debugEvent != null) {
            //Logger.Log("DBGEVENT: {0}", debugEvent.Value.DebugEventCode);

            var continueStatus = DebuggerApi.CONTINUE_STATUS.DBG_CONTINUE;
            bool result = DebuggerApi.ContinueDebugEvent(debugEvent.Value.ProcessId, debugEvent.Value.ThreadId,
                continueStatus);
            if (!result)
              Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
          }

          // Did we get ask to stop the debugger?
          if (this._stopWaitHandle.WaitOne(1)) {
            DebugStop();
            break;
          }
        }
      }
      catch (Exception e) {
        Logger.Log("Exception in debugger loop: {0}", e.Message);
      }
    }

    private static DebuggerApi.DEBUG_EVENT? WaitForDebugEvent(uint timeout) {
      DebuggerApi.DEBUG_EVENT debugEvent;
      bool result = DebuggerApi.WaitForDebugEvent(out debugEvent, timeout);
      if (!result) {
        int hr = Marshal.GetHRForLastWin32Error();
        if (hr == Win32.HR_ERROR_SEM_TIMEOUT)
          return null;

        Marshal.ThrowExceptionForHR(hr);
      }
      return debugEvent;
    }
  }
}
