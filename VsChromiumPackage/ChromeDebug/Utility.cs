// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using VsChromiumPackage.ChromeDebug.LowLevel;
using System.IO;

namespace VsChromiumPackage.ChromeDebug {
  static class Utility {
    public static bool IsChromeProcess(string imagePath) {
      string file = Path.GetFileName(imagePath);
      return file.Equals("chrome.exe", StringComparison.CurrentCultureIgnoreCase);
    }

    public static string[] SplitArgs(string unsplitArgumentLine) {
      if (unsplitArgumentLine == null)
        return new string[0];

      int numberOfArgs;
      IntPtr ptrToSplitArgs;
      string[] splitArgs;

      ptrToSplitArgs = NativeMethods.CommandLineToArgvW(unsplitArgumentLine, out numberOfArgs);

      // CommandLineToArgvW returns NULL upon failure.
      if (ptrToSplitArgs == IntPtr.Zero)
        throw new ArgumentException("Unable to split argument.", new Win32Exception());

      // Make sure the memory ptrToSplitArgs to is freed, even upon failure.
      try {
        splitArgs = new string[numberOfArgs];

        // ptrToSplitArgs is an array of pointers to null terminated Unicode strings.
        // Copy each of these strings into our split argument array.
        for (int i = 0; i < numberOfArgs; i++)
          splitArgs[i] = Marshal.PtrToStringUni(
            Marshal.ReadIntPtr(ptrToSplitArgs, i * IntPtr.Size));

        return splitArgs;
      }
      finally {
        // Free memory obtained by CommandLineToArgW.
        NativeMethods.LocalFree(ptrToSplitArgs);
      }
    }

    public static T ReadUnmanagedStructFromProcess<T>(IntPtr processHandle,
                                                      IntPtr addressInProcess) {
      int bytesToRead = Marshal.SizeOf(typeof(T));
      IntPtr buffer = Marshal.AllocHGlobal(bytesToRead);
      try {
        int bytesRead;
        if (!NativeMethods.ReadProcessMemory(processHandle, addressInProcess, buffer, bytesToRead,
                                             out bytesRead))
          throw new Win32Exception();
        T result = (T)Marshal.PtrToStructure(buffer, typeof(T));
        return result;
      }
      finally {
        Marshal.FreeHGlobal(buffer);
      }
    }

    public static string ReadStringUniFromProcess(IntPtr processHandle,
                                                  IntPtr addressInProcess,
                                                  int numChars) {
      int bytesRead;
      IntPtr outBuffer = Marshal.AllocHGlobal(numChars * 2);

      bool bresult = NativeMethods.ReadProcessMemory(processHandle,
                                                     addressInProcess,
                                                     outBuffer,
                                                     numChars * 2,
                                                     out bytesRead);
      if (!bresult)
        throw new Win32Exception();

      string result = Marshal.PtrToStringUni(outBuffer, bytesRead / 2);
      Marshal.FreeHGlobal(outBuffer);
      return result;
    }

    public static int UnmanagedStructSize<T>() {
      return Marshal.SizeOf(typeof(T));
    }
  }
}
