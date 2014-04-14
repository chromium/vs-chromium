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
  public class SearchContentsData : IDisposable {
    private readonly ParsedSearchString _parsedSearchString;
    private readonly IList<SearchContentsAlgorithms> _searchAlgorithms;

    public SearchContentsData(ParsedSearchString parsedSearchString, IList<SearchContentsAlgorithms> searchAlgorithms) {
      _parsedSearchString = parsedSearchString;
      _searchAlgorithms = searchAlgorithms;
    }

    /// <summary>
    /// The user provied search string split into sub-entries according to wildcards characters.
    /// </summary>
    public ParsedSearchString ParsedSearchString { get { return _parsedSearchString; } }

    /// <summary>
    /// The list of algorithms for each entry in "ParsedSearchString".
    /// </summary>
    //public IList<SearchContentsAlgorithms> SearchAlgorithms { get { return _searchAlgorithms; } }

    public SearchContentsAlgorithms GetSearchAlgorithms(ParsedSearchString.Entry entry) {
      return _searchAlgorithms[entry.Index];
    }

    public void Dispose() {
      foreach (var algo in _searchAlgorithms) {
        algo.Dispose();
      }
    }
  }
}
