// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Configuration;
using VsChromium.Core.Files;

namespace VsChromium.Server.Projects {
  public class SearchableFilesFilter : ISearchableFilesFilter {
    private readonly FilePatternsPathMatcherProvider _ignoreMatcherProvider;
    private readonly FilePatternsPathMatcherProvider _includeMatcherProvider;

    public SearchableFilesFilter(IConfigurationSectionContents ignoreSection, IConfigurationSectionContents includeSection) {
      _ignoreMatcherProvider = new FilePatternsPathMatcherProvider(ignoreSection);
      _includeMatcherProvider = new FilePatternsPathMatcherProvider(includeSection);
    }

    public bool Include(RelativePath fileName) {
      if (_ignoreMatcherProvider.AnyPathMatcher.MatchFileName(fileName, SystemPathComparer.Instance))
        return false;
      return _includeMatcherProvider.AnyPathMatcher.MatchFileName(fileName, SystemPathComparer.Instance);
    }
  }
}
