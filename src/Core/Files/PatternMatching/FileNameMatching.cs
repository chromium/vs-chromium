// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;
using System.Linq;

namespace VsChromium.Core.Files.PatternMatching {
  public class FileNameMatching {
    public static IPathMatcher ParsePattern(string pattern) {
      return new AggregatePathMatcher(Enumerable.Repeat(PatternParser.ParsePattern(pattern), 1));
    }

    public static bool IsPathSeparator(char ch) {
      return ch == Path.DirectorySeparatorChar;
    }
  }
}
