// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.FileNames.PatternMatching {
  public class OpRecursiveDir : BaseOperator {
    public override int MatchWorker(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex) {
      // If we are the last operation, don't match
      if (operatorIndex == operators.Count - 1)
        return -1;

      // If we reach the end of the "path", or we are not on a path separator, don't match
      if (pathIndex == path.Length || !FileNameMatching.IsPathSeparator(path[pathIndex]))
        return -1;

      pathIndex++;

      while (pathIndex < path.Length) {
        var result = Match(kind, comparer, operators, operatorIndex + 1, path, pathIndex);
        if (result == path.Length)
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
      return "**";
    }
  }
}
