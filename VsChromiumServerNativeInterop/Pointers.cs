// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromiumServer.NativeInterop {
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

    /// <summary>
    /// Returns the # of bytes between "start" end "end" (excluded).
    /// </summary>
    public static int Offset32(byte* start, byte* end) {
      if (start > end)
        throw new ArgumentException();

      var diff = (long)(end - start);
      if (diff > Int32.MaxValue)
        throw new ArgumentException();
      return (int)diff;
    }
  }
}
