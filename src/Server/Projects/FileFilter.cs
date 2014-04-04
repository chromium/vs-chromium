// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Configuration;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.Projects {
  public class FileFilter : IFileFilter {
    private readonly PathPatternsFile _ignorePatternsFile;

    public FileFilter(IConfigurationSectionProvider configurationSectionProvider) {
      _ignorePatternsFile = new PathPatternsFile(configurationSectionProvider, ConfigurationSectionNames.SourceExplorerIgnore);
    }

    public bool Include(string relativePath) {
      var ignore = _ignorePatternsFile.GetPathMatcher().MatchFileName(relativePath, SystemPathComparer.Instance);
      return !ignore;
    }
  }
}
