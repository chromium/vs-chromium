// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.CodeDom;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Abstraction over a single path component.
  /// </summary>
  public struct PathComponent : IComparable<PathComponent>, IEquatable<PathComponent> {
    private readonly string _path;
    private readonly int _index;
    private readonly int _count;
    private readonly PathComparisonOption _pathComparisonOption;

    public PathComponent(string path, int index, int count, PathComparisonOption pathComparisonOption) {
      _path = path;
      _index = index;
      _count = count;
      _pathComparisonOption = pathComparisonOption;
    }

    public bool Equals(PathComponent other) {
      return CompareTo(other) == 0;
    }

    public override bool Equals(object obj) {
      if (obj is PathComponent)
        return this.Equals((PathComponent)obj);
      return false;
    }

    public override int GetHashCode() {
      // TODO(rpaquay): Do this need to be optimized?
      int hash = 0;
      for (var i = 0; i < _count; i++) {
        char ch = _path[_index + i];
        if (_pathComparisonOption == PathComparisonOption.CaseInsensitive)
          ch = char.ToLowerInvariant(ch);
        hash = HashCode.Combine(hash, ch);
      }
      return hash;
    }

    public override string ToString() {
      return this.Value;
    }

    public string Value { get { return _path.Substring(_index, _count); } }

    public int CompareTo(PathComponent other) {
      return CompareSubStrings(this._path, this._index, this._count,
        other._path, other._index, other._count, _pathComparisonOption);
    }

    private static int CompareSubStrings(string strA, int indexA, int lengthA, string strB, int indexB, int lengthB, PathComparisonOption pathComparisonOption) {
      // Compare the common prefix of both strings.
      var comparison = (pathComparisonOption == PathComparisonOption.CaseInsensitive
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal);
      var count = Math.Min(lengthA, lengthB);
      int result = String.Compare(strA, indexA, strB, indexB, count, comparison);
      if (result != 0)
        return result;

      // String have a common prefix. The shortest string is then considred the
      // "less than" one.
      return lengthA - lengthB;
    }
  }
}