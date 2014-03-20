// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using VsChromium.Core.Win32;
using VsChromium.Core.Win32.Jobs;

namespace VsChromium.Core.JobObjects {
  public class JobObject : IDisposable {
    private SafeFileHandle _handle;

    public void AddCurrentProcess() {
      CreateJob();
      var result = NativeMethods.AssignProcessToJobObject(_handle, Process.GetCurrentProcess().Handle);
      if (!result)
        throw new LastWin32ErrorException("Error adding process to job");
    }

    public void Dispose() {
      if (_handle != null) {
        _handle.Dispose();
        _handle = null;
      }
    }

    private void CreateJob() {
      if (_handle == null)
        _handle = CreateJobHandle();
    }

    private static SafeFileHandle CreateJobHandle() {
      var handle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
      if (handle == null) {
        throw new LastWin32ErrorException("Error creating job handle");
      }

      var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION();
      info.LimitFlags = 0x2000;

      var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
      extendedInfo.BasicLimitInformation = info;

      int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
      var extendedInfoPtr = Marshal.AllocHGlobal(length);
      try {
        Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

        if (!NativeMethods.SetInformationJobObject(handle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length)) {
          throw new LastWin32ErrorException("Unable to set job information");
        }

        return handle;
      }
      finally {
        Marshal.FreeHGlobal(extendedInfoPtr);
      }
    }
  }
}
