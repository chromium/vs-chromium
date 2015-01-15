// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.NativeInterop {
  public class AsciiStringSearchRegex : AsciiStringSearchNative {
    public AsciiStringSearchRegex(string pattern, NativeMethods.SearchOptions searchOptions)
      : base(NativeMethods.SearchAlgorithmKind.kRegex, pattern, searchOptions) {
    }
  }
}