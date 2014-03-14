// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromiumServer.NativeInterop {
  public class AsciiStringSearchBndm64 : AsciiStringSearchNative {
    public AsciiStringSearchBndm64(string pattern, NativeMethods.SearchOptions searchOptions)
      : base(NativeMethods.SearchAlgorithmKind.kBndm64, pattern, searchOptions) {
      if (pattern.Length > 64)
        throw new ArgumentException("Bndm64 algorithm is limited to patterns of 64 characters maximum.", "pattern");
    }
  }
}
