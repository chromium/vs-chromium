// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files.PatternMatching {
  /// <summary>
  /// One element of a parsed pattern (see <see cref="PatternParser"/>). Operators match a relative
  /// path given as a <see cref="ReadOnlySpan{T}"/>, so that callers can match paths built in a
  /// reusable buffer, without allocating a string per path.
  /// </summary>
  public abstract class BaseOperator {
    /// <summary>
    /// Returns the index in <paramref name="path"/> after the match of the operators starting at
    /// <paramref name="operatorIndex"/>, or <code>-1</code> if there is no match.
    /// </summary>
    public abstract int MatchWorker(MatchKind kind, IPathComparer comparer, BaseOperator[] operators, int operatorIndex, ReadOnlySpan<char> path, int pathIndex);

    public static int Match(MatchKind kind, IPathComparer comparer, BaseOperator[] operators, int operatorIndex, ReadOnlySpan<char> path, int pathIndex) {
      if (operatorIndex > operators.Length)
        throw new ArgumentException("operator index outside of bounds.", "operatorIndex");
      if (pathIndex > path.Length)
        throw new ArgumentException("path index outside of bounds.", "pathIndex");

      if (operatorIndex == operators.Length)
        return pathIndex;

      return operators[operatorIndex].MatchWorker(kind, comparer, operators, operatorIndex, path, pathIndex);
    }
  }
}
