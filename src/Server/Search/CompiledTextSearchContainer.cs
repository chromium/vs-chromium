// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.NativeInterop;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Implementation of <see cref="ICompiledTextSearchContainer"/> that
  /// instantiates single instances of search algorithms.
  /// </summary>
  public class CompiledTextSearchContainer : ICompiledTextSearchContainer {
    private readonly ICompiledTextSearch _asciiCompiledTextSearchAlgo;
    private readonly ICompiledTextSearch _utf16CompiledTextSearchAlgo;

    public CompiledTextSearchContainer(string pattern, SearchProviderOptions searchOptions) {
      TextSearchFactory.CreateSearches(
        pattern,
        searchOptions.MatchCase,
        searchOptions.MatchWholeWord,
        searchOptions.UseRegex,
        out _asciiCompiledTextSearchAlgo,
        out _utf16CompiledTextSearchAlgo);
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
