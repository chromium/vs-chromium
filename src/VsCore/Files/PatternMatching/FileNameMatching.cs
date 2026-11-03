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
  }
}
