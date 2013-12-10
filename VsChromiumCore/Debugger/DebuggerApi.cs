// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Runtime.InteropServices;

namespace VsChromiumCore.Debugger {
  public static class Win32 {
    public const uint INFINITE = 0xFFFFFFFF;
    public const int HR_ERROR_SEM_TIMEOUT = unchecked((int)0x80070079);
    public const int HR_ERROR_NOT_SUPPORTED = unchecked((int)0x80070032);
  }

  public static class DebuggerApi {
    public enum CONTINUE_STATUS : uint {
      DBG_CONTINUE = 0x00010002,
      DBG_EXCEPTION_NOT_HANDLED = 0x80010001,
    }

    public enum DEBUG_EVENT_CODE : uint {
      EXCEPTION_DEBUG_EVENT = 1,
      CREATE_THREAD_DEBUG_EVENT = 2,
      CREATE_PROCESS_DEBUG_EVENT = 3,
      EXIT_THREAD_DEBUG_EVENT = 4,
      EXIT_PROCESS_DEBUG_EVENT = 5,
      LOAD_DLL_DEBUG_EVENT = 6,
      UNLOAD_DLL_DEBUG_EVENT = 7,
      OUTPUT_DEBUG_STRING_EVENT = 8,
      RIP_EVENT = 9,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_EVENT {
      public DEBUG_EVENT_CODE DebugEventCode;
      public int ProcessId;
      public int ThreadId;

      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
      public byte[] Data;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DebugActiveProcess(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DebugActiveProcessStop(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WaitForDebugEvent(out DEBUG_EVENT debugEvent, uint timeout);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ContinueDebugEvent(int processId, int threadId, CONTINUE_STATUS continuteStatus);
  }
}
