// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VsChromium.Core.Files.PatternMatching {
  public static class PatternParser {
    public static PathMatcher ParsePattern(string pattern) {
      if (pattern.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        throw new ArgumentException("Pattern should not contain alternative directory seperator character.", "pattern");

      return new PathMatcher(ParsePatternWorker(new PatternWrapper(pattern)));
    }

    private static readonly char PathSeparator = Path.DirectorySeparatorChar;
    private static string AnyDirMatchPrefix = "**" + PathSeparator;
    private static string AnyDirMatch = PathSeparator + "**" + PathSeparator;

    private static IEnumerable<BaseOperator> ParsePatternWorker(PatternWrapper pattern) {
      var result = new List<BaseOperator>();

      if (pattern.IsEmpty) {
        result.Add(new OpIsNoMatch());
        return result;
      }

      // Check for "/" suffix
      if (pattern.Last == PathSeparator) {
        result.Add(new OpIsDirectoryOnly());
        pattern.RemoveLast();
      }

      if (pattern.IsEmpty) {
        result.Add(new OpIsNoMatch());
        return result;
      }

      // Check for "/" prefix
      if (pattern.First == PathSeparator)
        pattern.Skip(1);
      else
        result.Add(new OpIsRelativeDirectory());

      // Check for "**/" prefix
      if (pattern.StartsWith(AnyDirMatchPrefix))
        pattern.Skip(3);

      if (pattern.IsEmpty) {
        result.Add(new OpIsNoMatch());
        return result;
      }

      var sb = new StringBuilder();
      Action addOpText = () => {
        if (sb.Length > 0) {
          result.Add(new OpText(sb.ToString()));
          sb.Clear();
        }
      };
      while (!pattern.IsEmpty) {
        if (pattern.StartsWith(AnyDirMatch)) {
          addOpText();
          result.Add(new OpRecursiveDir());
          pattern.Skip(4);
        } else if (pattern.First == PathSeparator) {
          addOpText();
          result.Add(new OpDirectorySeparator());
          pattern.Skip(1);
        } else if (pattern.First == '*') {
          addOpText();
          result.Add(new OpAsterisk());
          pattern.Skip(1);
        } else {
          sb.Append(pattern.First);
          pattern.Skip(1);
        }
      }
      addOpText();
      return result;
    }
  }
}
