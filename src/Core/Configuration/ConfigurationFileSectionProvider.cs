// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.FileNames;
using VsChromium.Server.Projects;

namespace VsChromium.Core.Configuration {
  public class ConfigurationFileSectionProvider : IConfigurationSectionProvider {
    private readonly IConfigurationFileProvider _configurationFileProvider;
    private readonly ConfigurationFileSectionProviderVolatileToken _volatileToken;

    public ConfigurationFileSectionProvider(IConfigurationFileProvider configurationFileProvider) {
      _configurationFileProvider = configurationFileProvider;
      _volatileToken = new ConfigurationFileSectionProviderVolatileToken();
    }

    public IEnumerable<string> GetSection(string sectionName, Func<IEnumerable<string>, IEnumerable<string>> postProcessing) {
      var filename = new RelativePathName(sectionName);
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
