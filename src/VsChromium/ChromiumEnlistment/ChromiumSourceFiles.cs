// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.IO;
using VsChromium.Core.Chromium;
using VsChromium.Core.Configuration;
using VsChromium.Core.FileNames;

namespace VsChromium.ChromiumEnlistment {
  [Export(typeof(IChromiumSourceFiles))]
  public class ChromiumSourceFiles : IChromiumSourceFiles {
    private readonly ConcurrentDictionary<string, bool> _applyCodingStyleResults =
      new ConcurrentDictionary<string, bool>(SystemPathComparer.Instance.Comparer);

    private readonly IPathPatternsFile _chromiumCodingStylePatterns;
    private readonly IChromiumDiscoveryWithCache<FullPath> _chromiumDiscoveryProvider;

    [ImportingConstructor]
    public ChromiumSourceFiles(IConfigurationFileProvider configurationFileProvider, IFileSystem fileSystem) {
      var configurationSectionProvider = new ConfigurationFileSectionProvider(configurationFileProvider);
      _chromiumCodingStylePatterns = new PathPatternsFile(configurationSectionProvider, ConfigurationStyleFilenames.ChromiumCodingStyleIgnore);
      _chromiumDiscoveryProvider = new ChromiumDiscoveryWithCache<FullPath>(configurationSectionProvider, fileSystem);
    }

    public void ValidateCache() {
      _applyCodingStyleResults.Clear();
    }

    public bool ApplyCodingStyle(string filename) {
      var path = new FullPath(filename);
      var root = _chromiumDiscoveryProvider.GetEnlistmentRootFromFilename(path, x => x);
      if (root == default(FullPath))
        return false;

      return _applyCodingStyleResults.GetOrAdd(filename, (key) => ApplyCodingStyleWorker(root, key));
    }

    private bool ApplyCodingStyleWorker(FullPath root, string filename) {
      var relativePath = filename.Substring(root.FullName.Length);
      if (relativePath.Length == 0)
        return false;

      if (relativePath[0] == Path.DirectorySeparatorChar)
        relativePath = relativePath.Substring(1);

      if (relativePath.Length == 0)
        return false;

      return !_chromiumCodingStylePatterns.GetPathMatcher().MatchFileName(new RelativePath(relativePath), SystemPathComparer.Instance);
    }
  }
}
