// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;

namespace VsChromiumCore.FileNames.PatternMatching {
  public class OpRelativeDirectory : BaseOperator {
    public override int MatchWorker(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex) {
      while (pathIndex < path.Length) {
        var result = Match(kind, comparer, operators, operatorIndex + 1, path, pathIndex);
        if (result >= 0)
          return result;

        // Look for next path separator in path
        var nextPathIndex = path.IndexOf(Path.DirectorySeparatorChar, pathIndex, path.Length - pathIndex);
        if (nextPathIndex < 0)
          break;
        pathIndex = nextPathIndex + 1;
      }

      return -1;
    }

    public override string ToString() {
      return "<relative dir>";
    }
  }
}
