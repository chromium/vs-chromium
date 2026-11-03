// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Text;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Win32.Strings {
  public static class Conversion {
    public static unsafe string Utf8ToString(byte* start, byte* end) {
      return new string((sbyte*)start, 0, (int)(end - start));
    }

    public static unsafe string Utf16ToString(byte* start, byte* end) {
      return new string((char*)start, 0, (int)((end - start) / sizeof(char)));
    }
  }
}
