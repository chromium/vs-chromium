// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Files.PatternMatching;

namespace VsChromium.Core.Configuration {
  /// <summary>
  /// Provider <see cref="IPathMatcher"/> implementation for a set of patterns.
  /// </summary>
  public interface IFilePatternsPathMatcherProvider {
    /// <summary>
    /// Return all entries contained in the collection.
    /// </summary>
    IEnumerable<IPathMatcher> PathMatcherEntries { get; }
  }
}
