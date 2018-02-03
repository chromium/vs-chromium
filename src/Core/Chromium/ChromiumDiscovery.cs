// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Files.PatternMatching;
using VsChromium.Core.Win32.Files;

namespace VsChromium.Core.Chromium {
  public class ChromiumDiscovery : IChromiumDiscovery {
    private readonly IFileSystem _fileSystem;
    private readonly IFilePatternsPathMatcherProvider _chromiumEnlistmentFilePatterns;

    public ChromiumDiscovery(IFileSystem fileSystem, IConfigurationSectionProvider configurationSectionProvider) {
      _fileSystem = fileSystem;
      var contents = ConfigurationSectionContents.Create(configurationSectionProvider, ConfigurationFileNames.ChromiumEnlistmentDetectionPatterns);
      _chromiumEnlistmentFilePatterns = new FilePatternsPathMatcherProvider(contents);
    }

    public void ValidateCache() {
      // Nothing to do
    }

    public FullPath? GetEnlistmentRootPath(FullPath path) {
      return path.EnumeratePaths()
        .Where(x => IsChromiumSourceDirectory(x, _chromiumEnlistmentFilePatterns))
        .Cast<FullPath?>()
        .FirstOrDefault();
    }

    private bool IsChromiumSourceDirectory(FullPath path, IFilePatternsPathMatcherProvider chromiumEnlistmentFilePatterns) {
      if (!_fileSystem.DirectoryExists(path)) {
        return false;
      }

      // We need to ensure that all pattern lines are covered by at least one file/directory of |path|.
      var entries = _fileSystem.GetDirectoryEntries(path);
      return chromiumEnlistmentFilePatterns.PathMatcherEntries
        .All(item => MatchFileOrDirectory(item, entries));
    }

    private static bool MatchFileOrDirectory(IPathMatcher item, IList<DirectoryEntry> entries) {
      return
        entries.Any(d => d.IsDirectory && item.MatchDirectoryName(new RelativePath(d.Name), SystemPathComparer.Instance)) ||
        entries.Any(f => f.IsFile && item.MatchFileName(new RelativePath(f.Name), SystemPathComparer.Instance));
    }
  }
}
