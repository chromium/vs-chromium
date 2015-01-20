// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class SearchContentsAlgorithms : IDisposable {
    private readonly AsciiStringSearchAlgorithm _asciiStringSearchAlgo;
    private readonly UTF16StringSearchAlgorithm _utf16StringSearchAlgo;

    public SearchContentsAlgorithms(string pattern, NativeMethods.SearchOptions searchOptions) {
      _asciiStringSearchAlgo = AsciiFileContents.CreateSearchAlgo(pattern, searchOptions);
      _utf16StringSearchAlgo = UTF16FileContents.CreateSearchAlgo(pattern, searchOptions);
    }

    public AsciiStringSearchAlgorithm AsciiStringSearchAlgo { get { return _asciiStringSearchAlgo; } }
    public UTF16StringSearchAlgorithm UTF16StringSearchAlgo { get { return _utf16StringSearchAlgo; } }

    public void Dispose() {
      _asciiStringSearchAlgo.Dispose();
      _utf16StringSearchAlgo.Dispose();
    }
  }
}