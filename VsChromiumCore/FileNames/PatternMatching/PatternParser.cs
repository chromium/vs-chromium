// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.FileNames.PatternMatching {
  public static class PatternParser {
    public static PathMatcher ParsePattern(string pattern) {
      if (pattern.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        throw new ArgumentException("Pattern should not contain alternative directory seperator character.", "pattern");

      return new PathMatcher(ParsePatternWorker(pattern));
    }

    private static IEnumerable<BaseOperator> ParsePatternWorker(string patternText) {
      var pattern = new PatternWrapper(patternText);

      if (pattern.IsEmpty)
        yield return new OpNoMatch();

      // Check for "/" suffix
      if (IsPathSeparator(pattern.Last)) {
        yield return new OpDirectoryOnly();
        pattern.RemoveLast();
      }

      if (pattern.IsEmpty)
        yield return new OpNoMatch();

      // Check for "/" prefix
      if (IsPathSeparator(pattern.First))
        pattern.Skip(1);
      else
        yield return new OpRelativeDirectory();

      // Check for "**/" prefix
      if (pattern.StartsWith("**\\"))
        pattern.Skip(3);

      if (pattern.IsEmpty)
        yield return new OpNoMatch();

      while (!pattern.IsEmpty) {
        var anyDirIndex = pattern.IndexOf("\\**\\");
        if (anyDirIndex >= 0) {
          if (anyDirIndex >= pattern.Index)
            yield return new OpText(pattern.Take(anyDirIndex - pattern.Index));
          yield return new OpRecursiveDir();
          pattern.Skip(4);
          continue;
        }

        var asteriskIndex = pattern.IndexOf("*");
        if (asteriskIndex >= 0) {
          if (asteriskIndex > pattern.Index)
            yield return new OpText(pattern.Take(asteriskIndex - pattern.Index));
          yield return new OpAsterisk();
          pattern.Skip(1);
        } else {
          yield return new OpText(pattern.Take(pattern.Remaining));
        }
      }
    }

    private static bool IsPathSeparator(char ch) {
      return ch == Path.DirectorySeparatorChar;
    }
  }
}
