// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files.PatternMatching;

namespace VsChromium.Core.Configuration {
  public class FilePatternsPathMatcherProvider : IFilePatternsPathMatcherProvider {
    private readonly IConfigurationSectionContents _sectionContents;
    private readonly Lazy<IPathMatcher> _matcher;
    private readonly Lazy<List<PathMatcher>> _matcherLines;

    public FilePatternsPathMatcherProvider(IConfigurationSectionContents contents) {
      _sectionContents = contents;
      _matcherLines = new Lazy<List<PathMatcher>>(CreateMatcherLines, LazyThreadSafetyMode.PublicationOnly);
      _matcher = new Lazy<IPathMatcher>(CreateMatcher, LazyThreadSafetyMode.PublicationOnly);
    }

    public IPathMatcher AnyPathMatcher {
      get { return _matcher.Value; }
    }

    public IEnumerable<IPathMatcher> PathMatcherEntries {
      get { return _matcherLines.Value; }
    }

    private List<PathMatcher> CreateMatcherLines() {
      var patterns = _sectionContents.Contents;
      return patterns.Select(PatternParser.ParsePattern).ToList();
    }

    private IPathMatcher CreateMatcher() {
      return new AnyPathMatcher(_matcherLines.Value);
    }
  }
}
