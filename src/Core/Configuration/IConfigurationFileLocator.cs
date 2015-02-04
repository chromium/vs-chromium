// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  /// <summary>
  /// Provides access to configuration files in various configuration directories.
  /// </summary>
  public interface IConfigurationFileLocator {
    /// <summary>
    /// Returns the contents of the configuration file named <paramref name="relativePath"/>.
    /// Throws an exception if the file cannot be found.
    /// </summary>
    IEnumerable<string> ReadFile(
      RelativePath relativePath,
      Func<FullPath, IEnumerable<string>, IEnumerable<string>> postProcessing);
  }
}
