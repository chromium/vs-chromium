// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VsChromium.Core.Files.PatternMatching {
  /// <summary>
  /// Implementation of <see cref="IPathMatcher"/> over an enumeration of
  /// existing  <see cref="IPathMatcher"/>, where the matching operation
  /// succeeds when at least one of the passed in matcher succeeds.
  /// </summary>
  public class AnyPathMatcher : IPathMatcher {
    private HashSet<string> _fileExtensions;
    private IPathMatcher[] _pathMatchers;

    public AnyPathMatcher(IEnumerable<PathMatcher> pathMatchers) {
      Optimize(pathMatchers.ToArray());
    }

    public bool MatchDirectoryName(RelativePath relativePath, IPathComparer comparer) {
      if (relativePath.IsEmpty)
        throw new ArgumentNullException("relativePath");
      var path = relativePath.Value;

      if (_fileExtensions.Contains(Path.GetExtension(path)))
        return true;

      // Note: Use "for" loop to avoid allocation if using "Any()"
      for (var index = 0; index < _pathMatchers.Length; index++) {
        if (_pathMatchers[index].MatchDirectoryName(relativePath, comparer))
          return true;
      }
      return false;
    }

    public bool MatchFileName(RelativePath relativePath, IPathComparer comparer) {
      if (relativePath.IsEmpty)
        throw new ArgumentNullException("relativePath");
      var path = relativePath.Value;

      if (_fileExtensions.Contains(Path.GetExtension(path)))
        return true;

      // Note: Use "for" loop to avoid allocation if using "Any()"
      for (var index = 0; index < _pathMatchers.Length; index++) {
        if (_pathMatchers[index].MatchFileName(relativePath, comparer))
          return true;
      }
      return false;
    }

    private void Optimize(IEnumerable<PathMatcher> matchers) {
      var pathMatchers = new List<IPathMatcher>();
      var fileExtensions = new HashSet<string>(SystemPathComparer.Instance.StringComparer);

      foreach (var x in matchers) {
        string fileExtension;
        if (IsFileExtension(x, out fileExtension)) {
          fileExtensions.Add(fileExtension);
        } else {
          pathMatchers.Add(x);
        }
      }

      _pathMatchers = pathMatchers.ToArray();
      _fileExtensions = fileExtensions;
    }

    private bool IsFileExtension(PathMatcher pathMatcher, out string ext) {
      ext = "";
      var result =
        pathMatcher.Operators.Count == 3 &&
        pathMatcher.Operators[0] is OpIsRelativeDirectory &&
        pathMatcher.Operators[1] is OpAsterisk &&
        pathMatcher.Operators[2] is OpText &&
        IsFileExtensionString(((OpText)pathMatcher.Operators[2]).Text);
      if (result)
        ext = ((OpText)pathMatcher.Operators[2]).Text;
      return result;
    }

    private bool IsFileExtensionString(string text) {
      if (text.Length < 2)
        return false;

      if (text[0] != '.')
        return false;

      for (var i = 1; i < text.Length; i++) {
        if (text[i] == '.')
          return false;
        if (text[i] == Path.DirectorySeparatorChar)
          return false;
        if (text[i] == Path.AltDirectorySeparatorChar)
          return false;
        if (text[i] == Path.VolumeSeparatorChar)
          return false;
        if (Path.GetInvalidFileNameChars().Contains(text[i]))
          return false;
      }
      return true;
    }
  }
}
