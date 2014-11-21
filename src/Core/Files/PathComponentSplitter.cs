// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections;
using System.Collections.Generic;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Splits a path into its components without allocating memory.
  /// </summary>
  public struct PathComponentSplitter : IEnumerable<PathComponent> {
    private readonly string _path;
    private readonly PathComparisonOption _pathComparisonOption;

    public PathComponentSplitter(string path, PathComparisonOption pathComparisonOption) {
      _path = path;
      _pathComparisonOption = pathComparisonOption;
    }

    public PathComponentEnumerator GetEnumerator() {
      return new PathComponentEnumerator(_path, _pathComparisonOption);
    }

    IEnumerator<PathComponent> IEnumerable<PathComponent>.GetEnumerator() {
      return this.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return this.GetEnumerator();
    }
  }
}