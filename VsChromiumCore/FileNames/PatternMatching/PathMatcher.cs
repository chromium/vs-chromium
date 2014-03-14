// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VsChromiumCore.FileNames.PatternMatching {
  public class PathMatcher : IPathMatcher {
    private readonly IList<BaseOperator> _operators;
    private readonly IPrePassWontMatch[] _prePassOperators;

    public PathMatcher(IEnumerable<BaseOperator> operators) {
      _operators = operators.ToArray();
      _prePassOperators = _operators.OfType<IPrePassWontMatch>().ToArray();
    }

    public IList<BaseOperator> Operators { get { return _operators; } }

    public bool MatchDirectoryName(string path, IPathComparer comparer) {
      CheckPath(path);

      if (PrePassWontMatch(MatchKind.Directory, path, comparer))
        return false;

      var result = BaseOperator.Match(MatchKind.Directory, comparer, Operators, 0, path, 0);
      return IsMatch(path, result);
    }

    public bool MatchFileName(string path, IPathComparer comparer) {
      CheckPath(path);

      if (PrePassWontMatch(MatchKind.File, path, comparer))
        return false;

      var result = BaseOperator.Match(MatchKind.File, comparer, Operators, 0, path, 0);
      return IsMatch(path, result);
    }

    private static void CheckPath(string path) {
      if (String.IsNullOrEmpty(path))
        throw new ArgumentNullException("path");

      // Note: The lines below show as using about 20% of total CPU when loading files.
      //  This is too much for ensuring program correctness.
#if false
      if (path.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        throw new ArgumentException(string.Format("Path \"{0}\" should not contain alternative directory seperator character.", path), "path");

      if (Path.IsPathRooted(path))
        throw new ArgumentException(string.Format("Path \"{0}\" should be relative (i.e. not rooted).", path), "path");
#else
      if (PathHelpers.IsAbsolutePath(path)) {
        throw new ArgumentException(string.Format("Path \"{0}\" should be relative (i.e. not rooted).", path), "path");
      }
#endif
    }

    private bool PrePassWontMatch(MatchKind kind, string path, IPathComparer comparer) {
      // Note: Use a "for" loop to avoid allocation with "Any"
      for (var index = 0; index < _prePassOperators.Length; index++) {
        if (_prePassOperators[index].PrePassWontMatch(kind, path, comparer))
          return true;
      }
      return false;
    }

    private static bool IsMatch(string path, int result) {
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
