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
    private HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _fileExtensionsLookup;
    private IPathMatcher[] _pathMatchers;

    public AnyPathMatcher(IEnumerable<PathMatcher> pathMatchers) {
      Optimize(pathMatchers.ToArray());
    }

    public bool MatchDirectoryName(ReadOnlySpan<char> relativePath, IPathComparer comparer) {
      if (relativePath.IsEmpty)
        throw new ArgumentNullException("relativePath");

      if (_fileExtensions.Count > 0 && _fileExtensionsLookup.Contains(GetExtension(relativePath)))
        return true;

      // Note: Use "for" loop to avoid allocation if using "Any()"
      for (var index = 0; index < _pathMatchers.Length; index++) {
        if (_pathMatchers[index].MatchDirectoryName(relativePath, comparer))
          return true;
      }
      return false;
    }

    public bool MatchFileName(ReadOnlySpan<char> relativePath, IPathComparer comparer) {
      if (relativePath.IsEmpty)
        throw new ArgumentNullException("relativePath");

      if (_fileExtensions.Count > 0 && _fileExtensionsLookup.Contains(GetExtension(relativePath)))
        return true;

      // Note: Use "for" loop to avoid allocation if using "Any()"
      for (var index = 0; index < _pathMatchers.Length; index++) {
        if (_pathMatchers[index].MatchFileName(relativePath, comparer))
          return true;
      }
      return false;
    }

    /// <summary>
    /// The extension of <paramref name="path"/> including the dot (e.g.
    /// ".txt"), or an empty span.
    /// </summary>
    private static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> path) {
      var index = path.LastIndexOf('.');
      return index < 0 ? ReadOnlySpan<char>.Empty : path.Slice(index);
    }

    private void Optimize(IEnumerable<PathMatcher> matchers) {
      var pathMatchers = new List<IPathMatcher>();
      // Note: Same equality as SystemPathComparer, with a comparer supporting span lookups.
      var fileExtensions = new HashSet<string>(
        PathComparerRegistry.Default.Comparison == StringComparison.Ordinal ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

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
      _fileExtensionsLookup = fileExtensions.GetAlternateLookup<ReadOnlySpan<char>>();
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
