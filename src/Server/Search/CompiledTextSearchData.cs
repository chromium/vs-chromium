// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Container for various pieces of data needed by all text search components.
  /// TODO(rpaquay): It would be nicer to make this a little bit more OO and decouple.
  /// </summary>
  public class CompiledTextSearchData : IDisposable {
    private readonly ParsedSearchString _parsedSearchString;
    private readonly IList<ICompiledTextSearchProvider> _searchAlgorithms;

    public CompiledTextSearchData(ParsedSearchString parsedSearchString, IList<ICompiledTextSearchProvider> searchAlgorithms) {
      _parsedSearchString = parsedSearchString;
      _searchAlgorithms = searchAlgorithms;
    }

    /// <summary>
    /// The user provied search string split into sub-entries according to wildcards characters.
    /// </summary>
    public ParsedSearchString ParsedSearchString { get { return _parsedSearchString; } }

    public ICompiledTextSearchProvider GetSearchAlgorithmProvider(ParsedSearchString.Entry entry) {
      return _searchAlgorithms[entry.Index];
    }

    public void Dispose() {
      foreach (var algo in _searchAlgorithms) {
        algo.Dispose();
      }
    }
  }
}
