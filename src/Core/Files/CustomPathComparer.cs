// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using VsChromium.Core.Logging;

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

    public int IndexOf(string value, string searchText, int index, int count) {
      return value.IndexOf(searchText, index, count, _comparison);
    }

    public override int Compare(string x, string y) {
      x = (x ?? string.Empty);
      y = (y ?? string.Empty);
      return Compare(x, 0, y, 0, Math.Max(x.Length, y.Length));
    }

    public int Compare(string value1, int startIndex1, string value2, int startIndex2, int length) {
      if (object.ReferenceEquals(value1, value2))
        return 0;

      if (value1 == null)
        return -1;

      if (value2 == null)
        return 1;

      while (true) {
        var index1 = value1.IndexOf(Path.DirectorySeparatorChar, startIndex1);
        var index2 = value2.IndexOf(Path.DirectorySeparatorChar, startIndex2);
        bool outside1 = (index1 < 0) || (index1 >= startIndex1 + length);
        bool outside2 = (index2 < 0) || (index2 >= startIndex2 + length);
        if (outside1 || outside2) {
          int len1 = (index1 < 0 ? value1.Length : index1) - startIndex1;
          len1 = Math.Min(length, len1);
          int len2 = (index2 < 0 ? value2.Length : index2) - startIndex2;
          len2 = Math.Min(length, len2);
          var result = CompareSubStrings(
            value1, startIndex1, len1,
            value2, startIndex2, len2);
          if (result != 0)
            return result;
          if (outside1 && outside2)
            return 0;
          if (outside1)
            return -1;
          Invariants.Assert(outside2);
          return 1;
        } else {
          int len1 = index1 - startIndex1;
          int len2 = index2 - startIndex2;
          var result = CompareSubStrings(
            value1, startIndex1, len1,
            value2, startIndex2, len2);
          if (result != 0)
            return result;

          Invariants.Assert(len1 == len2);
          length -= (len1 + 1);
          startIndex1 = index1 + 1;
          startIndex2 = index2 + 1;
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