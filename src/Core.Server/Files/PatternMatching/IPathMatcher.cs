// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files.PatternMatching {
  /// <summary>
  /// Matches relative paths (e.g. "foo\bar\baz.cc") against a pattern. The paths are passed as a
  /// <see cref="ReadOnlySpan{T}"/>, so that callers can build them in a reusable buffer instead of
  /// allocating a string per path.
  /// </summary>
  public interface IPathMatcher {
    bool MatchDirectoryName(ReadOnlySpan<char> path, IPathComparer comparer);
    bool MatchFileName(ReadOnlySpan<char> path, IPathComparer comparer);
  }

  public static class PathMatcherExtensions {
    public static bool MatchDirectoryName(this IPathMatcher matcher, RelativePath path, IPathComparer comparer) {
      return matcher.MatchDirectoryName(path.Value.AsSpan(), comparer);
    }

    public static bool MatchFileName(this IPathMatcher matcher, RelativePath path, IPathComparer comparer) {
      return matcher.MatchFileName(path.Value.AsSpan(), comparer);
    }
  }
}
