// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Implementation of <see cref="ICompiledTextSearchContainer"/> that
  /// instantiates single instances of search algorithms.
  /// </summary>
  public class CompiledTextSearchContainer : ICompiledTextSearchContainer {
    private readonly ICompiledTextSearch _asciiCompiledTextSearchAlgo;
    private readonly ICompiledTextSearch _utf16CompiledTextSearchAlgo;

    public CompiledTextSearchContainer(string pattern, SearchProviderOptions searchOptions) {
      _asciiCompiledTextSearchAlgo = AsciiFileContents.CreateSearchAlgo(pattern, searchOptions);
      _utf16CompiledTextSearchAlgo = Utf16FileContents.CreateSearchAlgo(pattern, searchOptions);
    }

    public ICompiledTextSearch GetAsciiSearch() {
      return _asciiCompiledTextSearchAlgo;
    }

    public ICompiledTextSearch GetUtf16Search() {
      return _utf16CompiledTextSearchAlgo;
    }

    public void Dispose() {
      _asciiCompiledTextSearchAlgo.Dispose();
      _utf16CompiledTextSearchAlgo.Dispose();
    }
  }
}