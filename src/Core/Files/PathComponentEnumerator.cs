// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Implementation of <see cref="PathComponentSplitter"/> enumerator.
  /// Note: This is a stuct (to avoid heap allocations) with mutable fields.
  /// This can always lead to unexpected behavior, but this is similar to the
  /// pattern used in BCL collections.
  /// </summary>
  public struct PathComponentEnumerator : IEnumerator<PathComponent> {
    private readonly PathComparisonOption _pathComparisonOption;

    private static readonly char[] _directorySeparators = {
      Path.DirectorySeparatorChar,
      Path.AltDirectorySeparatorChar
    };
    private readonly string _path;
    private int _index;
    private int _count;

    public PathComponentEnumerator(string path, PathComparisonOption pathComparisonOption) {
      _pathComparisonOption = pathComparisonOption;
      _path = path ?? string.Empty;
      _index = -1;
      _count = 0;
    }

    public PathComponent Current {
      get {
        return new PathComponent(_path, _index, _count, _pathComparisonOption);
      }
    }

    public void Dispose() {
    }

    object IEnumerator.Current {
      get { return this.Current; }
    }

    public bool MoveNext() {
      if (this._index < 0) {
        _index = 0;
      } else {
        _index += _count + 1; // Skip past directory separator.
      }

      // ">=" because we may have skipped past a terminating directory
      // separator.
      if (this._index >= this._path.Length)
        return false;

      // Look for next directory separator.
      var separatorIndex = _path.IndexOfAny(_directorySeparators, _index);
      if (separatorIndex < 0)
        separatorIndex = _path.Length;

      _count = separatorIndex - _index;
      return true;
    }

    public void Reset() {
      this._index = -1;
    }
  }
}
