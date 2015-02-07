// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;

namespace VsChromium.Core.Configuration {
  /// <summary>
  /// Implementation of <see cref="IConfigurationSectionProvider"/> from
  /// configuration files located on disk.
  /// </summary>
  public class ConfigurationFileConfigurationSectionProvider : IConfigurationSectionProvider {
    private readonly IConfigurationFileLocator _configurationFileLocator;

    public ConfigurationFileConfigurationSectionProvider(IConfigurationFileLocator configurationFileLocator) {
      _configurationFileLocator = configurationFileLocator;
    }

    public IConfigurationSectionContents GetSection(string sectionName, Func<IEnumerable<string>, IEnumerable<string>> postProcessing) {
      var filename = new RelativePath(sectionName);
      var contents = _configurationFileLocator.ReadFile(filename, (path, lines) => postProcessing(lines)).ToReadOnlyCollection();
      return new ConfigurationSectionContents(sectionName, contents);
    }
  }
}
