// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  /// <summary>
  /// Implementation of <see cref="IConfigurationSectionProvider"/> using a file
  /// fo each named section.
  /// </summary>
  public class ConfigurationFileSectionProvider : IConfigurationSectionProvider {
    private readonly IConfigurationFileProvider _configurationFileProvider;

    public ConfigurationFileSectionProvider(IConfigurationFileProvider configurationFileProvider, IFileSystem fileSystem) {
      _configurationFileProvider = configurationFileProvider;
    }

    public IEnumerable<string> GetSection(string sectionName, Func<IEnumerable<string>, IEnumerable<string>> postProcessing) {
      var filename = new RelativePath(sectionName);
      return _configurationFileProvider.ReadFile(filename, (fullPathName, lines) => postProcessing(lines));
    }
  }
}
