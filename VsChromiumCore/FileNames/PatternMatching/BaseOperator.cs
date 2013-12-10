// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromiumCore.FileNames.PatternMatching {
  public abstract class BaseOperator {
    public abstract int MatchWorker(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex);

    /// <summary>
    /// Returns -1 if "path" does not conform to the pattern defined by "operators"
    /// Returns the index of the first character "path" which does not match the pattern.
    /// This means that if "index" == "path.Length", the whole path matches the pattern.
    /// </summary>
    public static int Match(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex) {
      if (operatorIndex > operators.Count)
        throw new ArgumentException("operator index outside of bounds.", "operatorIndex");

      if (pathIndex > path.Length)
        throw new ArgumentException("path index outside of bounds.", "pathIndex");

      // If we reach past the last operator, it means "we matched until this point".
      if (operatorIndex == operators.Count)
        return pathIndex;

      return operators[operatorIndex].MatchWorker(kind, comparer, operators, operatorIndex, path, pathIndex);
    }
  }
}
