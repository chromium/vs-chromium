// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Configuration {
  public class ConfigurationFileLocator : IConfigurationFileLocator {
    private readonly IFileSystem _fileSystem;

    public ConfigurationFileLocator(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public IEnumerable<string> ReadFile(
      RelativePath relativePath,
      Func<FullPath, IEnumerable<string>, IEnumerable<string>> postProcessing) {
      foreach (var directoryName in PossibleDirectoryNames()) {
        var path = directoryName.Combine(relativePath);
        if (_fileSystem.FileExists(path)) {
          Logger.LogInfo("Using configuration file at \"{0}\"", path);
          return postProcessing(path, _fileSystem.ReadAllLines(path)).ToReadOnlyCollection();
        }
      }

      throw new FileLoadException(
        string.Format("Could not load configuration file \"{0}\" from the following locations:{1}", relativePath,
                      PossibleDirectoryNames().Aggregate("", (x, y) => x + "\n" + y)));
    }

    private IEnumerable<FullPath> PossibleDirectoryNames() {
      yield return new FullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        .Combine(new RelativePath(ConfigurationDirectoryNames.LocalUserConfigurationDirectoryName));

      // Note: Assembly.Location is empty when the server is published as a
      // single file executable: use the application directory instead.
      var installDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
      yield return new FullPath(installDirectory)
        .Combine(new RelativePath(ConfigurationDirectoryNames.LocalInstallConfigurationDirectoryName));
    }
  }
}
