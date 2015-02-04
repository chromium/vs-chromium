// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Threading;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Implementation of <see cref="ICompiledTextSearchProvider"/> that
  /// instantiates search algorithms per thread instance to avoid lock
  /// contention.
  /// </summary>
  public class PerThreadCompiledTextSearchProvider : ICompiledTextSearchProvider {
    private readonly string _pattern;
    private readonly SearchProviderOptions _searchOptions;
    private readonly ConcurrentDictionary<int, ICompiledTextSearch> _asciiAlgorithms = new ConcurrentDictionary<int, ICompiledTextSearch>();
    private readonly Func<int, ICompiledTextSearch> _asciiAlgorithmFactory;
    private readonly ICompiledTextSearch _unicodeCompiledTextSearchAlgo;

    public PerThreadCompiledTextSearchProvider(string pattern, SearchProviderOptions searchOptions) {
      _pattern = pattern;
      _searchOptions = searchOptions;
      _asciiAlgorithmFactory = x => AsciiFileContents.CreateSearchAlgo(_pattern, _searchOptions);
      _unicodeCompiledTextSearchAlgo = Utf16FileContents.CreateSearchAlgo(_pattern, _searchOptions);

      // Force execution on the current thread so that we get an exception if
      // the search engine finds "pattern" is invalid.
      this.GetAsciiSearch();
    }

    public ICompiledTextSearch GetAsciiSearch() {
      return _asciiAlgorithms.GetOrAdd(Thread.CurrentThread.ManagedThreadId, _asciiAlgorithmFactory);
    }

    public ICompiledTextSearch GetUtf16Search() {
      return _unicodeCompiledTextSearchAlgo;
    }

    public void Dispose() {
      foreach (var algo in _asciiAlgorithms.Values) {
        algo.Dispose();
      }
      _asciiAlgorithms.Clear();
      _unicodeCompiledTextSearchAlgo.Dispose();
    }
  }
}