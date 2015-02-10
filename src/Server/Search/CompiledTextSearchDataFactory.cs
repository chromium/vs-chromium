// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using VsChromium.Core.Files.PatternMatching;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Search {
  [Export(typeof(ICompiledTextSearchDataFactory))]
  public class CompiledTextSearchDataFactory : ICompiledTextSearchDataFactory {
    private const int MinimumSearchPatternLength = 2;
    private readonly ISearchStringParser _searchStringParser;
    private readonly ICompiledTextSearchProviderFactory _compiledTextSearchProviderFactory;

    [ImportingConstructor]
    public CompiledTextSearchDataFactory(
      ISearchStringParser searchStringParser,
      ICompiledTextSearchProviderFactory compiledTextSearchProviderFactory) {
      _searchStringParser = searchStringParser;
      _compiledTextSearchProviderFactory = compiledTextSearchProviderFactory;
    }

    public CompiledTextSearchData Create(SearchParams searchParams, Func<FileName, bool> fileNamePathMatcher) {
      ParsedSearchString parsedSearchString;
      if (searchParams.Regex) {
        parsedSearchString = new ParsedSearchString(
          new ParsedSearchString.Entry { Text = searchParams.SearchString },
          Enumerable.Empty<ParsedSearchString.Entry>(),
          Enumerable.Empty<ParsedSearchString.Entry>());
      } else {
        var str = searchParams.SearchString ?? "";
        if (str.Length < MinimumSearchPatternLength) {
          throw new RecoverableErrorException(
            string.Format(
              "Search pattern must contain at least {0} characters",
              MinimumSearchPatternLength));
        }
        parsedSearchString = _searchStringParser.Parse(searchParams.SearchString ?? "");
      }

      var searchContentsAlgorithms = CreateSearchAlgorithms(
        parsedSearchString,
        new SearchProviderOptions {
          MatchCase = searchParams.MatchCase,
          MatchWholeWord = searchParams.MatchWholeWord,
          UseRegex = searchParams.Regex,
          UseRe2Engine = searchParams.UseRe2Engine
        });

      return new CompiledTextSearchData(
        parsedSearchString,
        searchContentsAlgorithms,
        fileNamePathMatcher);
    }

    private List<ICompiledTextSearchContainer> CreateSearchAlgorithms(
      ParsedSearchString parsedSearchString, SearchProviderOptions options) {
      return parsedSearchString.EntriesBeforeMainEntry
        .Concat(new[] { parsedSearchString.MainEntry })
        .Concat(parsedSearchString.EntriesAfterMainEntry)
        .OrderBy(x => x.Index)
        .Select(entry => _compiledTextSearchProviderFactory.CreateProvider(entry.Text, options))
        .ToList();
    }
  }
}