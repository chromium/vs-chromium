// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.FileNames.PatternMatching;

namespace VsChromium.Core.Configuration {
  public interface IPathPatternsFile {
    IPathMatcher GetPathMatcher();
    IEnumerable<IPathMatcher> GetPathMatcherLines();
  }
}
