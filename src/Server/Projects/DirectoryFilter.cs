// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Configuration;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.Projects {
  public class DirectoryFilter : IDirectoryFilter {
    private readonly PathPatternsFile _ignorePatternsFile;

    public DirectoryFilter(IConfigurationSectionProvider configurationSectionProvider) {
      _ignorePatternsFile = new PathPatternsFile(configurationSectionProvider, ConfigurationSectionNames.SourceExplorerIgnore);
    }

    public bool Include(RelativePathName relativePath) {
      var ignore = _ignorePatternsFile.GetPathMatcher().MatchDirectoryName(relativePath.RelativeName, SystemPathComparer.Instance);
      return !ignore;
    }
  }
}
