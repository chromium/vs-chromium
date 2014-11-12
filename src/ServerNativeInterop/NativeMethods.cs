// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace VsChromium.Server.NativeInterop {
  public static class NativeMethods {
    public enum SearchAlgorithmKind {
      kStrStr = 1,
      kBndm32 = 2,
      kBndm64 = 3,
      kBoyerMoore = 4
    }

    [Flags]
    public enum SearchOptions {
      kNone = 0x0000,
      kMatchCase = 0x0001
    }

    public enum TextKind {
      Ascii,
      AsciiWithUtf8Bom,
      Utf8WithBom,
      Unknown
    }

    [SuppressUnmanagedCodeSecurity]
    [DllImport("VsChromium.Native.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern SafeSearchHandle AsciiSearchAlgorithm_Create(
      SearchAlgorithmKind kind,
      IntPtr pattern,
      int patternLen,
      SearchOptions options);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("VsChromium.Native.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern IntPtr AsciiSearchAlgorithm_Search(SafeSearchHandle handle, IntPtr text, int textLen);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("VsChromium.Native.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern void AsciiSearchAlgorithm_Delete(IntPtr handle);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("VsChromium.Native.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern TextKind Text_GetKind(IntPtr text, int textLen);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("VsChromium.Native.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
      SetLastError = false)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Ascii_Compare(IntPtr text1, long text1Length, IntPtr text2, long text2Length);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("VsChromium.Native.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
      SetLastError = false)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Ascii_GetLineExtentFromPosition(IntPtr text, int textLen, int position, out int lineStartPosition, out int lineLength);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("VsChromium.Native.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
      SetLastError = false)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool UTF16_GetLineExtentFromPosition(IntPtr text, int textLen, int position, out int lineStartPosition, out int lineLength);

  }
}
