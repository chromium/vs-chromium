// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Linq;

namespace VsChromium.Core.Configuration {
  /// <summary>
  /// Implementation of <see cref="IConfigurationSectionProvider"/> using a
  /// single file with multiple sections.
  /// </summary>
  public class FileWithSectionConfigurationProvider : IConfigurationSectionProvider {
    private readonly IFileWithSections _fileWithSections;

    public FileWithSectionConfigurationProvider(IFileWithSections fileWithSections) {
      _fileWithSections = fileWithSections;
    }

    public IConfigurationSectionContents GetSection(string sectionName, Func<IEnumerable<string>, IEnumerable<string>> postProcessing) {
      return new ConfigurationSectionContents(
        sectionName,
        _fileWithSections.ReadSection(sectionName, postProcessing).ToReadOnlyCollection());
    }
  }
}
