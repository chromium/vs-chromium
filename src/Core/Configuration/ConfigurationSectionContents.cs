// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Configuration {
  public class ConfigurationSectionContents : IConfigurationSectionContents {
    private readonly string _name;
    private readonly IList<string> _contents;

    public ConfigurationSectionContents(string name, IList<string> contents) {
      // TODO(rpaquay): Find way to invalidate cache.
      _name = name;
      _contents = contents;
    }

    public string Name { get { return _name; } }
    public IList<string> Contents { get { return _contents; } }
  }
}