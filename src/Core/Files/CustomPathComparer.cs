// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Implements <see cref="StringComparer"/> for file system paths.
  /// </summary>
  public class CustomPathComparer : StringComparer {
    private readonly PathComparisonOption _pathComparisonOption;

    public CustomPathComparer(PathComparisonOption pathComparisonOption) {
      _pathComparisonOption = pathComparisonOption;
    }

    public override int Compare(string x, string y) {
      if (object.ReferenceEquals(x, y))
        return 0;

      if (x == null)
        return -1;

      if (y == null)
        return 1;

      // Enumerate path components of both string and compare them in order.
      //
      // This is required to allow sorting paths with common prefix properly.
      // For example, "foo/bar" should be "less than" "foo-1/bar". Using string
      // litteral comparison would give an incorrect result. This also allows
      // ignoring path separator inconsistencies when comparing paths, e.g.
      // "foo\bar" should be "equal to" "foo/bar".
      var ex = new PathComponentEnumerator(x, _pathComparisonOption);
      var ey = new PathComponentEnumerator(y, _pathComparisonOption);
      while (true) {
        var xdone = !ex.MoveNext();
        var ydone = !ey.MoveNext();
        if (xdone || ydone) {
          if (xdone == ydone)
            return 0;
          if (xdone)
            return -1;
          return 1;
        }

        int result = ex.Current.CompareTo(ey.Current);
        if (result != 0)
          return result;
      }
    }

    public override bool Equals(string x, string y) {
      return Compare(x, y) == 0;
    }

    public override int GetHashCode(string obj) {
      if (string.IsNullOrEmpty(obj))
        return 0;

      var e = new PathComponentEnumerator(obj, _pathComparisonOption);
      int hash = 0;
      while (e.MoveNext()) {
        hash = HashCode.Combine(hash, e.Current.GetHashCode());
      }
      return hash;
    }
  }
}