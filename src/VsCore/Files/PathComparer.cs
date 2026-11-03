// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  public class PathComparer : IPathComparer {
    private readonly CustomPathComparer _pathComparer;

    public PathComparer(PathComparisonOption option) {
      _pathComparer = new CustomPathComparer(option);
    }

    public StringComparer StringComparer { get { return _pathComparer; } }

    public int Compare(string value1, int startIndex1, string value2, int startIndex2, int length) {
      return _pathComparer.Compare(value1, startIndex1, value2, startIndex2, length);
    }

    public int IndexOf(string value, string searchText, int index, int count) {
      return _pathComparer.IndexOf(value, searchText, index, count);
    }
  }
}
