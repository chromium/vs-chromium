// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Core.Configuration {
  /// <summary>
  /// Abstraction over either file or project file implementation of configuration
  /// "sections", i.e. set of text lines grouped into a section with a given name.
  /// </summary>
  public interface IConfigurationSectionProvider {
    IEnumerable<string> GetSection(string name, Func<IEnumerable<string>, IEnumerable<string>> postProcessing);
  }
}
