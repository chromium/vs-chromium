// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromiumCore.FileNames.PatternMatching;

namespace VsChromiumCore.Configuration {
  public class PathPatternsFile : IPathPatternsFile {
    private readonly string _configurationFileName;
    private readonly IConfigurationSectionProvider _configurationSectionProvider;
    private readonly Lazy<IPathMatcher> _matcher;
    private readonly Lazy<List<PathMatcher>> _matcherLines;

    public PathPatternsFile(IConfigurationSectionProvider configurationSectionProvider, string configurationFileName) {
      _configurationSectionProvider = configurationSectionProvider;
      _configurationFileName = configurationFileName;
      _matcherLines = new Lazy<List<PathMatcher>>(CreateMatcherLines, LazyThreadSafetyMode.PublicationOnly);
      _matcher = new Lazy<IPathMatcher>(CreateMatcher, LazyThreadSafetyMode.PublicationOnly);
    }

    public IPathMatcher GetPathMatcher() {
      return _matcher.Value;
    }

    public IEnumerable<IPathMatcher> GetPathMatcherLines() {
      return _matcherLines.Value;
    }

    private List<PathMatcher> CreateMatcherLines() {
      var patterns = _configurationSectionProvider.GetSection(_configurationFileName, FilterDirectories);
      return patterns.Select(x => PatternParser.ParsePattern(x)).ToList();
    }

    private IPathMatcher CreateMatcher() {
      return new AggregatePathMatcher(_matcherLines.Value);
    }

    private IEnumerable<string> FilterDirectories(IEnumerable<string> arg) {
      // Note: The file is user input, so we need to replace "/" with "\".
      return arg
        .Where(line => !IsCommentLine(line))
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => line.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
    }

    private static bool IsCommentLine(string x) {
      return x.TrimStart().StartsWith("#");
    }
  }
}
