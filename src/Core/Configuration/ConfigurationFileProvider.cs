// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  [Export(typeof(IConfigurationFileProvider))]
  public class ConfigurationFileProvider : IConfigurationFileProvider {
    private readonly IFileSystem _fileSystem;

    [ImportingConstructor]
    public ConfigurationFileProvider(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public IEnumerable<string> ReadFile(RelativePath relativePath, Func<FullPath, IEnumerable<string>, IEnumerable<string>> postProcessing) {
      foreach (var directoryName in PossibleDirectoryNames()) {
        var path = directoryName.Combine(relativePath);
        if (_fileSystem.FileExists(path))
          return postProcessing(path, _fileSystem.ReadAllLines(path));
      }

      throw new FileLoadException(
        string.Format("Could not load configuration file \"{0}\" from the following locations:{1}", relativePath,
                      PossibleDirectoryNames().Aggregate("", (x, y) => x + "\n" + y)));
    }

    private IEnumerable<FullPath> PossibleDirectoryNames() {
      yield return new FullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        .Combine(new RelativePath(ConfigurationDirectoryNames.LocalUserConfigurationDirectoryName));

      yield return new FullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
        .Combine(new RelativePath(ConfigurationDirectoryNames.LocalInstallConfigurationDirectoryName));
    }
  }
}
