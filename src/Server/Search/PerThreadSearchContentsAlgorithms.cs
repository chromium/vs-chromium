// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Threading;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class PerThreadSearchContentsAlgorithms : ISearchContentsAlgorithms {
    private readonly string _pattern;
    private readonly NativeMethods.SearchOptions _searchOptions;
    private readonly ConcurrentDictionary<int, AsciiStringSearchAlgorithm> _asciiAlgorithms = new ConcurrentDictionary<int, AsciiStringSearchAlgorithm>();
    private readonly Func<int, AsciiStringSearchAlgorithm> _asciiAlgorithmFactory;
    private readonly UTF16StringSearchAlgorithm _unicodeStringSearchAlgo;

    public PerThreadSearchContentsAlgorithms(string pattern, NativeMethods.SearchOptions searchOptions) {
      _pattern = pattern;
      _searchOptions = searchOptions;
      _asciiAlgorithmFactory = x => AsciiFileContents.CreateSearchAlgo(_pattern, _searchOptions);
      _unicodeStringSearchAlgo = UTF16FileContents.CreateSearchAlgo(_pattern, _searchOptions);
    }

    public AsciiStringSearchAlgorithm GetAsciiStringSearchAlgo() {
      return _asciiAlgorithms.GetOrAdd(Thread.CurrentThread.ManagedThreadId, _asciiAlgorithmFactory);
    }

    public UTF16StringSearchAlgorithm GetUnicodeStringSearchAlgo() {
      return _unicodeStringSearchAlgo;
    }

    public void Dispose() {
      foreach (var algo in _asciiAlgorithms.Values) {
        algo.Dispose();
      }
      _asciiAlgorithms.Clear();
      _unicodeStringSearchAlgo.Dispose();
    }
  }
}