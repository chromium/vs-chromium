// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.NativeInterop {
  public class AsciiStringSearchRe2 : AsciiStringSearchNative {
    public AsciiStringSearchRe2(string pattern, NativeMethods.SearchOptions searchOptions)
      : base(NativeMethods.SearchAlgorithmKind.kRe2, pattern, searchOptions) {
    }
  }
}