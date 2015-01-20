// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class SearchContentsAlgorithms : ISearchContentsAlgorithms {
    private readonly AsciiStringSearchAlgorithm _asciiStringSearchAlgo;
    private readonly UTF16StringSearchAlgorithm _unicodeStringSearchAlgo;

    public SearchContentsAlgorithms(string pattern, NativeMethods.SearchOptions searchOptions) {
      _asciiStringSearchAlgo = AsciiFileContents.CreateSearchAlgo(pattern, searchOptions);
      _unicodeStringSearchAlgo = UTF16FileContents.CreateSearchAlgo(pattern, searchOptions);
    }

    public AsciiStringSearchAlgorithm GetAsciiStringSearchAlgo() {
      return _asciiStringSearchAlgo;
    }

    public UTF16StringSearchAlgorithm GetUnicodeStringSearchAlgo() {
      return _unicodeStringSearchAlgo;
    }

    public void Dispose() {
      _asciiStringSearchAlgo.Dispose();
      _unicodeStringSearchAlgo.Dispose();
    }
  }
}