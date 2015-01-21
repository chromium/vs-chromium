// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Threading;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class PerThreadCompiledTextSearchProvider : ICompiledTextSearchProvider {
    private readonly string _pattern;
    private readonly SearchProviderOptions _searchOptions;
    private readonly ConcurrentDictionary<int, AsciiCompiledTextSearch> _asciiAlgorithms = new ConcurrentDictionary<int, AsciiCompiledTextSearch>();
    private readonly Func<int, AsciiCompiledTextSearch> _asciiAlgorithmFactory;
    private readonly Utf16CompiledTextSearch _unicodeCompiledTextSearchAlgo;

    public PerThreadCompiledTextSearchProvider(string pattern, SearchProviderOptions searchOptions) {
      _pattern = pattern;
      _searchOptions = searchOptions;
      _asciiAlgorithmFactory = x => AsciiFileContents.CreateSearchAlgo(_pattern, _searchOptions);
      _unicodeCompiledTextSearchAlgo = Utf16FileContents.CreateSearchAlgo(_pattern, _searchOptions);
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