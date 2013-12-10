// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VsChromiumCore.JobObjects {
  public enum JobObjectInfoType {
    AssociateCompletionPortInformation = 7,
    BasicLimitInformation = 2,
    BasicUIRestrictions = 4,
    EndOfJobTimeInformation = 6,
    ExtendedLimitInformation = 9,
    SecurityLimitInformation = 5,
    GroupInformation = 11
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct SECURITY_ATTRIBUTES {
    public int nLength;
    public IntPtr lpSecurityDescriptor;
    public int bInheritHandle;
  }

  [StructLayout(LayoutKind.Sequential)]
  struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
    public Int64 PerProcessUserTimeLimit;
    public Int64 PerJobUserTimeLimit;
    public Int16 LimitFlags;
    public UInt32 MinimumWorkingSetSize;
    public UInt32 MaximumWorkingSetSize;
    public Int16 ActiveProcessLimit;
    public Int64 Affinity;
    public Int16 PriorityClass;
    public Int16 SchedulingClass;
  }

  [StructLayout(LayoutKind.Sequential)]
  struct IO_COUNTERS {
    public UInt64 ReadOperationCount;
    public UInt64 WriteOperationCount;
    public UInt64 OtherOperationCount;
    public UInt64 ReadTransferCount;
    public UInt64 WriteTransferCount;
    public UInt64 OtherTransferCount;
  }

  [StructLayout(LayoutKind.Sequential)]
  struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
    public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
    public IO_COUNTERS IoInfo;
    public UInt32 ProcessMemoryLimit;
    public UInt32 JobMemoryLimit;
    public UInt32 PeakProcessMemoryUsed;
    public UInt32 PeakJobMemoryUsed;
  }

  public class JobObject : IDisposable {
    private readonly SafeFileHandle _handle;

    public JobObject() {
      _handle = CreateJobObject(IntPtr.Zero, null);

      var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION();
      info.LimitFlags = 0x2000;

      var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
      extendedInfo.BasicLimitInformation = info;

      int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
      var extendedInfoPtr = Marshal.AllocHGlobal(length);
      Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

      if (
        !SetInformationJobObject(_handle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr,
                                 (uint)length))
        throw new Exception(string.Format("Unable to set information.  Error: {0}", Marshal.GetLastWin32Error()));
    }

    public void Dispose() {
      _handle.Dispose();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateJobObject(IntPtr lpJobAttributes, string lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(
      SafeFileHandle hJob,
      JobObjectInfoType infoType,
      IntPtr lpJobObjectInfo,
      uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsProcessInJob(IntPtr processHandle, IntPtr jobHandle, out bool result);

    public bool AddProcessHandle(IntPtr processHandle) {
      return AssignProcessToJobObject(_handle, processHandle);
    }

    public static bool IsProcessInJob(Process process) {
      var handle = process.Handle;
      bool result;
      if (!IsProcessInJob(handle, IntPtr.Zero, out result))
        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

      return result;
    }

    public void AddProcess(IntPtr processHandle) {
      var result = AddProcessHandle(processHandle);
      if (!result)
        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
    }
  }
}
