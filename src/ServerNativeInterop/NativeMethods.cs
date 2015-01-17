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
      kBoyerMoore = 4,
      kRegex = 5,
    }

    [Flags]
    public enum SearchOptions {
      kNone = 0x0000,
      kMatchCase = 0x0001,
      kRegex = 0x0002
    }

    public enum TextKind {
      Ascii,
      AsciiWithUtf8Bom,
      Utf8WithBom,
      Unknown
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SearchParams {
      public IntPtr TextStart;
      public int TextLength;
      public IntPtr MatchStart;
      public int MatchLength;
      public IntPtr SearchBuffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SearchCreateResult {
      public int HResult;
      public fixed byte ErrorMessage [128];
    }

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern SafeSearchHandle AsciiSearchAlgorithm_Create(
      SearchAlgorithmKind kind,
      IntPtr pattern,
      int patternLen,
      SearchOptions options,
      [Out]out SearchCreateResult result);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern Int32 AsciiSearchAlgorithm_GetSearchBufferSize(
      SafeSearchHandle handle);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern void AsciiSearchAlgorithm_Search(
      SafeSearchHandle handle,
      ref SearchParams searchParams);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern void AsciiSearchAlgorithm_CancelSearch(
      SafeSearchHandle handle,
      ref SearchParams searchParams);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern void AsciiSearchAlgorithm_Delete(IntPtr handle);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    public static extern TextKind Text_GetKind(IntPtr text, int textLen);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Ascii_Compare(
      IntPtr text1,
      long text1Length,
      IntPtr text2,
      long text2Length);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Ascii_GetLineExtentFromPosition(
      IntPtr text,
      int textLen,
      int position,
      int maxOffset,
      out int lineStartPosition,
      out int lineLength);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(
      "VsChromium.Native.dll",
      CallingConvention = CallingConvention.StdCall,
      CharSet = CharSet.Ansi,
      SetLastError = false)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool UTF16_GetLineExtentFromPosition(
      IntPtr text,
      int textLen,
      int position,
      int maxOffset,
      out int lineStartPosition,
      out int lineLength);
  }
}
