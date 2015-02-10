// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Container for various pieces of data needed by all text search components.
  /// TODO(rpaquay): It would be nicer to make this a little bit more OO and decouple.
  /// </summary>
  public class CompiledTextSearchData : IDisposable {
    private readonly ParsedSearchString _parsedSearchString;
    private readonly IList<ICompiledTextSearchContainer> _searchContainers;
    private readonly Func<FileName, bool> _fileNameFilter;

    public CompiledTextSearchData(
      ParsedSearchString parsedSearchString,
      IList<ICompiledTextSearchContainer> searchContainers,
      Func<FileName, bool> fileNameFilter) {
      _parsedSearchString = parsedSearchString;
      _searchContainers = searchContainers;
      _fileNameFilter = fileNameFilter;
    }

    /// <summary>
    /// The user provied search string split into sub-entries according to
    /// wildcards characters.
    /// </summary>
    public ParsedSearchString ParsedSearchString {
      get {
        return _parsedSearchString;
      }
    }

    /// <summary>
    /// Function used to filter file names that should not be part of the text
    /// search.
    /// </summary>
    public Func<FileName, bool> FileNameFilter {
      get { return _fileNameFilter; }
    }

    /// <summary>
    /// Retrieve the <see cref="ICompiledTextSearchContainer"/> for a given
    /// search entry.
    /// </summary>
    public ICompiledTextSearchContainer GetSearchContainer(
      ParsedSearchString.Entry entry) {
      return _searchContainers[entry.Index];
    }

    /// <summary>
    /// Release the text search provider.
    /// </summary>
    public void Dispose() {
      foreach (var provider in _searchContainers) {
        provider.Dispose();
      }
    }
  }
}
