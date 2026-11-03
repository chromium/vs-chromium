// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Files;

namespace VsChromium.Core.Configuration {
  public interface IConfigurationSectionContents {
    /// <summary>
    /// The <see cref="FullPath"/> of the file containing this configuration section
    /// </summary>
    FullPath ContainingFilePath { get; }

    /// <summary>
    /// The name of this configuration section. It may be empty if the configuration
    /// section is the whole file contents (e.g. for Chromium projects)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The contents of the section, after removing comments
    /// </summary>
    IList<string> Contents { get; }
  }
}