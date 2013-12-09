// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;

namespace VsChromiumCore.FileNames.PatternMatching {
  /// <summary>
  /// Matches "*" inside a sub-path component (e.g. foo/*/bar).
  /// </summary>
  public class OpAsterisk : BaseOperator {
    public override int MatchWorker(MatchKind kind, IPathComparer comparer, IList<BaseOperator> operators, int operatorIndex, string path, int pathIndex) {
      // Heuristic: If last operator, there is a full match (since "*" at the end matches everything)
      if (operatorIndex == operators.Count - 1)
        return path.Length;

      // Heuristic:
      // * if we are matching a file name
      // * if there are not path separators after "pathIndex"
      // * if the only operator after us is "OpText", and
      // * then we only need to check that path end with the text.
      if (kind == MatchKind.File) {
        if (path.IndexOf(Path.DirectorySeparatorChar, pathIndex) < 0) {
          if (operatorIndex + 1 == operators.Count - 1) {
            var opText = operators[operatorIndex + 1] as OpText;
            if (opText != null) {
              if (path.EndsWith(opText.Text, comparer.Comparison)) {
                var remaining = path.Length - pathIndex;
                if (remaining >= opText.Text.Length)
                  return path.Length;
              }
              return -1;
            }
          }
        }
      }

      // Full "*" semantics (recursive...)
      for (var i = pathIndex; i < path.Length; i++) {
        // Stop at first path separator
        if (FileNameMatching.IsPathSeparator(path[i]))
          break;

        var result = Match(kind, comparer, operators, operatorIndex + 1, path, i);
        if (result == path.Length)
          return result;
      }

      return -1;
    }

    public override string ToString() {
      return "*";
    }
  }
}
