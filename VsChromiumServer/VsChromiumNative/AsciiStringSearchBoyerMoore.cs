// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumServer.VsChromiumNative {
  public class AsciiStringSearchBoyerMoore : AsciiStringSearchNative {
    public AsciiStringSearchBoyerMoore(string pattern, NativeMethods.SearchOptions searchOptions)
      : base(NativeMethods.SearchAlgorithmKind.kBoyerMoore, pattern, searchOptions) {
    }
  }
}
