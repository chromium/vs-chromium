// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;

using VsChromium.Core.Win32.Processes;

namespace VsChromium.Core.Utility {
  public static class MarshalUtility {
    public static T ReadUnmanagedStructFromProcess<T>(SafeProcessHandle processHandle,
                                                      IntPtr addressInProcess) {
      int bytesToRead = Marshal.SizeOf(typeof(T));
      IntPtr buffer = Marshal.AllocHGlobal(bytesToRead);
      byte[] dest = new byte[bytesToRead];
      try {
        uint bytesRead;
        if (!VsChromium.Core.Win32.Processes.NativeMethods.ReadProcessMemory(
                processHandle, addressInProcess, dest, (uint)bytesToRead, out bytesRead))
          throw new Win32Exception();
        Marshal.Copy(dest, 0, buffer, (int)bytesRead);
        T result = (T)Marshal.PtrToStructure(buffer, typeof(T));
        return result;
      } finally {
        Marshal.FreeHGlobal(buffer);
      }
    }

    public static string ReadStringUniFromProcess(SafeProcessHandle processHandle,
                                                  IntPtr addressInProcess,
                                                  int numChars) {
      uint bytesRead;
      IntPtr outBuffer = Marshal.AllocHGlobal(numChars * 2);
      byte[] buffer = new byte[numChars * 2];

      try {
        bool bresult = VsChromium.Core.Win32.Processes.NativeMethods.ReadProcessMemory(processHandle,
                                                       addressInProcess,
                                                       buffer,
                                                       (uint)(numChars * 2),
                                                       out bytesRead);
        if (!bresult)
          throw new Win32Exception();
        Marshal.Copy(buffer, 0, outBuffer, (int)bytesRead);
        return Marshal.PtrToStringUni(outBuffer, (int)(bytesRead / 2));
      } finally {
        Marshal.FreeHGlobal(outBuffer);
      }
    }

    public static int UnmanagedStructSize<T>() {
      return Marshal.SizeOf(typeof(T));
    }
  }
}
