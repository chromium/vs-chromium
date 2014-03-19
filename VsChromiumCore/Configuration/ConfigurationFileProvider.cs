// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VsChromium.Core.Configuration {
  [Export(typeof(IConfigurationFileProvider))]
  public class ConfigurationFileProvider : IConfigurationFileProvider {
    private const string _configurationDirectoryName = "Configuration";
    private const string _localConfigurationDirectoryName = ".VsChromium";

    public IEnumerable<string> ReadFile(string name, Func<IEnumerable<string>, IEnumerable<string>> postProcessing) {
      foreach (var directoryName in PossibleDirectoryNames()) {
        var path = Path.Combine(directoryName, name);
        if (File.Exists(path))
          return postProcessing(File.ReadAllLines(path));
      }

      throw new FileLoadException(
        string.Format("Could not load configuration file \"{0}\" from the following locations:{1}", name,
                      PossibleDirectoryNames().Aggregate("", (x, y) => x + "\n" + y)));
    }

    private IEnumerable<string> PossibleDirectoryNames() {
      yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), _localConfigurationDirectoryName);
      yield return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), _configurationDirectoryName);
    }
  }
}
