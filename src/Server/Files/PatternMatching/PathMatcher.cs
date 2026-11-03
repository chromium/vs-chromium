// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Files.PatternMatching {
  public class PathMatcher : IPathMatcher {
    private readonly BaseOperator[] _operators;
    private readonly IPrePassWontMatch[] _prePassOperators;

    public PathMatcher(IEnumerable<BaseOperator> operators) {
      _operators = operators.ToArray();
      _prePassOperators = _operators.OfType<IPrePassWontMatch>().ToArray();
    }

    public IList<BaseOperator> Operators { get { return _operators; } }

    public bool MatchDirectoryName(ReadOnlySpan<char> path, IPathComparer comparer) {
      CheckPath(path);

      if (PrePassWontMatch(MatchKind.Directory, path, comparer))
        return false;

      var result = BaseOperator.Match(MatchKind.Directory, comparer, _operators, 0, path, 0);
      return IsMatch(path, result);
    }

    public bool MatchFileName(ReadOnlySpan<char> path, IPathComparer comparer) {
      CheckPath(path);

      if (PrePassWontMatch(MatchKind.File, path, comparer))
        return false;

      var result = BaseOperator.Match(MatchKind.File, comparer, _operators, 0, path, 0);
      return IsMatch(path, result);
    }

    private static void CheckPath(ReadOnlySpan<char> path) {
      if (path.IsEmpty)
        throw new ArgumentNullException("path");

      // Note: The lines below show as using about 20% of total CPU when loading files.
      //  This is too much for ensuring program correctness: only checked in debug builds.
#if DEBUG
      if (PathHelpers.IsAbsolutePath(path.ToString())) {
        throw new ArgumentException(string.Format("Path \"{0}\" should be relative (i.e. not rooted).", path.ToString()), "path");
      }
#endif
    }

    private bool PrePassWontMatch(MatchKind kind, ReadOnlySpan<char> path, IPathComparer comparer) {
      // Note: Use a "for" loop to avoid allocation with "Any"
      for (var index = 0; index < _prePassOperators.Length; index++) {
        if (_prePassOperators[index].PrePassWontMatch(kind, path, comparer))
          return true;
      }
      return false;
    }

    private static bool IsMatch(ReadOnlySpan<char> path, int result) {
      if (result < -1 || result > path.Length)
        throw new InvalidOperationException("Invalid result! (Bug)");

      if (result == -1)
        return false;

      if (result == path.Length)
        return true;

      if (FileNameMatching.IsPathSeparator(path[result]))
        return true;

      return false;
    }
  }
}
