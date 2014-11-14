// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Core.Files {
  public class CaseInsensitivePathComparer : IPathComparer {
    private static readonly CaseInsensitivePathComparer _theInstance = new CaseInsensitivePathComparer();

    public static IPathComparer Instance { get { return _theInstance; } }

    public StringComparer Comparer { get { return CustomPathComparer.Instance; } }

    public StringComparison Comparison { get { return StringComparison.OrdinalIgnoreCase; } }
  }

  public class CustomPathComparer : StringComparer {
    private static readonly CustomPathComparer _theInstance = new CustomPathComparer();
    private static readonly char[] _directorySeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    public static CustomPathComparer Instance { get { return _theInstance; } }

    private static int CompareSubStrings(string strA, int indexA, int lengthA, string strB, int indexB, int lengthB) {
      int result = string.Compare(strA, indexA, strB, indexB, Math.Min(lengthA, lengthB), StringComparison.OrdinalIgnoreCase);
      if (result != 0)
        return result;

      // String have a common prefix. The shortest one is the smallest one.
      return lengthA - lengthB;
    }

    public override int Compare(string x, string y) {
      if (object.ReferenceEquals(x, y))
        return 0;

      // Compare individual components, using "\" as seperator.
      int startIndex = 0;
      int length = Math.Min(x.Length, y.Length);
      while (startIndex < length) {
        var idx = x.IndexOfAny(_directorySeparators, startIndex);
        var idy = y.IndexOfAny(_directorySeparators, startIndex);
        if (idx < 0) {
          if (idy < 0) {
            // No more path separator, compare the last component of both strings
            return CompareSubStrings(x, startIndex, x.Length - startIndex, y, startIndex, y.Length - startIndex);
          } else {
            // y has one additional component => x < y
            return -1;
          }
        } else {
          if (idy < 0) {
            // x has one additional component => x > y
            return 1;
          } else {
            var result = CompareSubStrings(x, startIndex, idx - startIndex, y, startIndex, idy - startIndex);
            if (result != 0)
              return result;
            startIndex = idx + 1;
          }
        }
      }

      // All common characters are equal to this point, use length
      // as final decision point.
      return x.Length - y.Length;
    }

    public override bool Equals(string x, string y) {
      return StringComparer.OrdinalIgnoreCase.Equals(x, y);
    }

    public override int GetHashCode(string obj) {
      return StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
    }
  }
}
