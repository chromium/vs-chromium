// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromiumCore.Configuration {
  public class FileWithSectionConfigurationProvider : IConfigurationSectionProvider {
    private readonly IFileWithSections _fileWithSections;

    public FileWithSectionConfigurationProvider(IFileWithSections fileWithSections) {
      _fileWithSections = fileWithSections;
    }

    public IEnumerable<string> GetSection(string name, Func<IEnumerable<string>, IEnumerable<string>> postProcessing) {
      return _fileWithSections.ReadSection(name, postProcessing);
    }
  }
}
