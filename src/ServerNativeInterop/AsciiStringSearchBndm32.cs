// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.NativeInterop {
  public class AsciiStringSearchBndm32 : AsciiStringSearchNative
  {
    public AsciiStringSearchBndm32(string pattern, NativeMethods.SearchOptions searchOptions)
      : base(NativeMethods.SearchAlgorithmKind.kBndm32, pattern, searchOptions) {
      if (pattern.Length > 32)
        throw new ArgumentException("Bndm32 algorithm is limited to patterns of 32 characters maximum.", "pattern");
    }
  }
}
