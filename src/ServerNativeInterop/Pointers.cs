// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.NativeInterop {
  public static unsafe class Pointers {
    public static byte* Add(byte* ptr, int offset) {
      return checked(ptr + offset);
    }

    public static byte* Add(byte* ptr, long offset) {
      return checked(ptr + offset);
    }

    public static byte* Add(IntPtr ptr, int offset) {
      return Add((byte*)ptr, offset);
    }

    public static byte* Add(IntPtr ptr, long offset) {
      return Add((byte*)ptr, offset);
    }

    public static IntPtr AddPtr(IntPtr pointer, int offset) {
      return pointer + offset;
    }

    public static IntPtr AddPtr(IntPtr pointer, long offset) {
      return new IntPtr(pointer.ToInt64() + offset);
    }

    /// <summary>
    /// Returns the # of bytes between "start" end "end" (excluded).
    /// </summary>
    public static long Offset64(byte* start, byte* end) {
      if (start > end)
        throw new ArgumentException();

      return Diff64(end, start);
    }

    /// <summary>
    /// Returns the # of bytes between "start" end "end" (excluded).
    /// </summary>
    public static long Offset64(IntPtr start, IntPtr end) {
      return Offset64((byte*)start.ToPointer(), (byte*)end.ToPointer());
    }

    /// <summary>
    /// Returns the # of bytes between "start" end "end" (excluded).
    /// </summary>
    public static long Diff64(byte* start, byte* end) {
      return start - end;
    }

    /// <summary>
    /// Returns the # of bytes between "start" end "end" (excluded).
    /// </summary>
    public static long Diff64(IntPtr start, IntPtr end) {
      return Diff64((byte*)start.ToPointer(), (byte*)end.ToPointer());
    }

    /// <summary>
    /// Returns the # of bytes between "start" end "end" (excluded).
    /// </summary>
    public static int Offset32(byte* start, byte* end) {
      var offset = Offset64(start, end);
      return checked((int)offset);
    }

    /// <summary>
    /// Returns the # of bytes between "start" end "end" (excluded).
    /// </summary>
    public static int Offset32(IntPtr start, IntPtr end) {
      return Offset32((byte*)start.ToPointer(), (byte*)end.ToPointer());
    }
  }
}
