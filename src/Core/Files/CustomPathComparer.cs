// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Implements <see cref="StringComparer"/> for file system paths.
  /// </summary>
  public class CustomPathComparer : StringComparer {
    private readonly StringComparison _comparison;
    private readonly StringComparer _comparer;

    public CustomPathComparer(PathComparisonOption pathComparisonOption) {
      _comparison = (pathComparisonOption == PathComparisonOption.CaseInsensitive
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal);
      _comparer = (pathComparisonOption == PathComparisonOption.CaseInsensitive
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal);
    }

    public override int Compare(string x, string y) {
      if (object.ReferenceEquals(x, y))
        return 0;

      if (x == null)
        return -1;

      if (y == null)
        return 1;

      var startIndex = 0;
      while (true) {
        var xindex = x.IndexOf(Path.DirectorySeparatorChar, startIndex);
        var yindex = y.IndexOf(Path.DirectorySeparatorChar, startIndex);
        if (xindex < 0 || yindex < 0) {
          var result = CompareSubStrings(
            x, startIndex, (xindex < 0 ? x.Length : xindex) - startIndex,
            y, startIndex, (yindex < 0 ? y.Length : yindex) - startIndex);
          if (result != 0)
            return result;
          if (xindex < 0 && yindex < 0)
            return 0;
          if (xindex < 0)
            return -1;
          return 1;
        } else {
          var result = CompareSubStrings(
            x, startIndex, xindex - startIndex,
            y, startIndex, yindex - startIndex);
          if (result != 0)
            return result;

          Debug.Assert(xindex == yindex);
          startIndex = xindex + 1;
        }
      }
    }

    private int CompareSubStrings(string strA, int indexA, int lengthA, string strB, int indexB, int lengthB) {
      // Compare the common prefix of both strings.
      var count = Math.Min(lengthA, lengthB);
      int result = String.Compare(strA, indexA, strB, indexB, count, _comparison);
      if (result != 0)
        return result;

      // String have a common prefix. The shortest string is then considered the
      // "less than" one.
      return lengthA - lengthB;
    }

    public override bool Equals(string x, string y) {
      return _comparer.Equals(x, y);
    }

    public override int GetHashCode(string obj) {
      return _comparer.GetHashCode(obj);
    }
  }
}