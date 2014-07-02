// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Caching;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  public class ConfigurationFileSectionProvider : IConfigurationSectionProvider {
    private readonly IConfigurationFileProvider _configurationFileProvider;
    private readonly ConfigurationFileSectionProviderVolatileToken _volatileToken;

    public ConfigurationFileSectionProvider(IConfigurationFileProvider configurationFileProvider, IFileSystem fileSystem) {
      _configurationFileProvider = configurationFileProvider;
      _volatileToken = new ConfigurationFileSectionProviderVolatileToken(fileSystem);
    }

    public IEnumerable<string> GetSection(string sectionName, Func<IEnumerable<string>, IEnumerable<string>> postProcessing) {
      var filename = new RelativePath(sectionName);
      return _configurationFileProvider.ReadFile(filename, (fullPathName, lines) => {
        _volatileToken.AddFile(fullPathName);
        return postProcessing(lines);
      });
    }

    public IVolatileToken WhenUpdated() {
      return _volatileToken;
    }
  }
}
