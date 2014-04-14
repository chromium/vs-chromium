// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class SearchContentsAlgorithms : IDisposable {
    private readonly AsciiStringSearchAlgorithm _asciiStringSearchAlgo;
    private readonly UTF16StringSearchAlgorithm _utf16StringSearchAlgo;

    public SearchContentsAlgorithms(AsciiStringSearchAlgorithm asciiStringSearchAlgo, UTF16StringSearchAlgorithm utf16StringSearchAlgo) {
      if (asciiStringSearchAlgo == null)
        throw new ArgumentNullException("asciiStringSearchAlgo");
      if (utf16StringSearchAlgo == null)
        throw new ArgumentNullException("utf16StringSearchAlgo");

      _asciiStringSearchAlgo = asciiStringSearchAlgo;
      _utf16StringSearchAlgo = utf16StringSearchAlgo;
    }

    public AsciiStringSearchAlgorithm AsciiStringSearchAlgo { get { return _asciiStringSearchAlgo; } }
    public UTF16StringSearchAlgorithm UTF16StringSearchAlgo { get { return _utf16StringSearchAlgo; } }

    public void Dispose() {
      _asciiStringSearchAlgo.Dispose();
      _utf16StringSearchAlgo.Dispose();
    }
  }
}