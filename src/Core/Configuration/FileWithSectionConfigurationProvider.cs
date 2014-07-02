// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Caching;

namespace VsChromium.Core.Configuration {
  public class FileWithSectionConfigurationProvider : IConfigurationSectionProvider {
    private readonly IFileWithSections _fileWithSections;

    public FileWithSectionConfigurationProvider(IFileWithSections fileWithSections) {
      _fileWithSections = fileWithSections;
    }

    public IEnumerable<string> GetSection(string sectionName, Func<IEnumerable<string>, IEnumerable<string>> postProcessing) {
      return _fileWithSections.ReadSection(sectionName, postProcessing);
    }

    public IVolatileToken WhenUpdated() {
      return _fileWithSections.WhenFileUpdated();
    }
  }
}
