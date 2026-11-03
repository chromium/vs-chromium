// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Core.Files.PatternMatching {
  /// <summary>
  /// Matches any character except "/"
  /// </summary>
  public class OpAsterisk : BaseOperator {
    public override int MatchWorker(MatchKind kind, IPathComparer comparer, BaseOperator[] operators, int operatorIndex, ReadOnlySpan<char> path, int pathIndex) {
      // Heuristic: If last operator, there is a full match (since "*" at the
      // end matches everything)
      if (operatorIndex == operators.Length - 1) {
        if (path.Slice(pathIndex).IndexOf(Path.DirectorySeparatorChar) < 0)
          return path.Length;
        return -1;
      }

      // Heuristic for "*[a-z]+":
      // * if we are matching a file name and
      // * if there are not path separators after "pathIndex" and
      // * if the only operator after us is "OpText"
      // * then we only need to check that path end with the text.
      if (kind == MatchKind.File) {
        if (path.Slice(pathIndex).IndexOf(Path.DirectorySeparatorChar) < 0) {
          if (operatorIndex + 1 == operators.Length - 1) {
            var opText = operators[operatorIndex + 1] as OpText;
            if (opText != null) {
              // Note: The native server version checked that the *first* occurrence of the text
              // was at the end of the path (see "Behavior changes" in PORTING_PRINCIPLES.md).
              if (path.EndsWith(opText.Text.AsSpan(), comparer.Comparison)) {
                var remaining = path.Length - pathIndex;
                if (remaining >= opText.Text.Length)
                  return path.Length;
              }
              return -1;
            }
          }
        }
      }

      // Full "*" semantics: any character except "/"
      for (var i = pathIndex; i < path.Length; i++) {
        // If we reach "/", move on to next operator
        if (FileNameMatching.IsPathSeparator(path[i]))
          return Match(kind, comparer, operators, operatorIndex + 1, path, i);

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
