// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Linq;

namespace VsChromium.Core.Files.PatternMatching {
  public class FileNameMatching {
    public static IPathMatcher ParsePattern(string pattern) {
      pattern = pattern ?? "";
      var patterns = pattern.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
      return new AnyPathMatcher(patterns.Select(PatternParser.ParsePattern));
    }

    public static bool IsPathSeparator(char ch) {
      return ch == Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Returns the index of the first directory separator of <paramref name="path"/> at or after
    /// <paramref name="startIndex"/>, or <code>-1</code>.
    /// </summary>
    public static int IndexOfPathSeparator(ReadOnlySpan<char> path, int startIndex) {
      var index = path.Slice(startIndex).IndexOf(Path.DirectorySeparatorChar);
      return index < 0 ? -1 : startIndex + index;
    }
  }
}
