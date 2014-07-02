// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.FileNames;

namespace VsChromium.Core.Configuration {
  public interface IConfigurationFileProvider {
    IEnumerable<string> ReadFile(RelativePath name, Func<FullPathName, IEnumerable<string>, IEnumerable<string>> postProcessing);
  }
}
