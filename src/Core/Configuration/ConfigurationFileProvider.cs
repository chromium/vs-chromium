// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using VsChromium.Core.FileNames;

namespace VsChromium.Core.Configuration {
  [Export(typeof(IConfigurationFileProvider))]
  public class ConfigurationFileProvider : IConfigurationFileProvider {
    private const string _configurationDirectoryName = "Configuration";
    private const string _localConfigurationDirectoryName = ".VsChromium";

    public IEnumerable<string> ReadFile(RelativePath name, Func<FullPathName, IEnumerable<string>, IEnumerable<string>> postProcessing) {
      foreach (var directoryName in PossibleDirectoryNames()) {
        var path = directoryName.Combine(name.Value);
        if (path.FileExists)
          return postProcessing(path, File.ReadAllLines(path.FullName));
      }

      throw new FileLoadException(
        string.Format("Could not load configuration file \"{0}\" from the following locations:{1}", name,
                      PossibleDirectoryNames().Aggregate("", (x, y) => x + "\n" + y)));
    }

    private IEnumerable<FullPathName> PossibleDirectoryNames() {
      yield return new FullPathName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Combine(_localConfigurationDirectoryName);
      yield return new FullPathName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).Combine(_configurationDirectoryName);
    }
  }
}
