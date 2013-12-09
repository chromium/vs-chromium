// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromiumCore.Configuration {
  public interface IFileWithSections {
    IEnumerable<string> ReadSection(string name, Func<IEnumerable<string>, IEnumerable<string>> postProcessing);
  }
}
