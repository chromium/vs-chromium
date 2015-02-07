// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Configuration {
  public interface IConfigurationSectionContents {
    string Name { get; }
    IList<string> Contents { get; }
  }
}