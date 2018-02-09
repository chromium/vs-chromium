// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Wraps a string representing the full path of a file or directory.
  /// </summary>
  public struct FullPath : IEquatable<FullPath>, IComparable<FullPath> {
    private readonly string _path;

    public FullPath(string path) {
      Invariants.CheckArgument(IsValid(path), nameof(path), "Path must be absolute: \"{0}\".", path);
      _path = path;
    }

    public static bool IsValid(string path) {
      return PathHelpers.IsAbsolutePath(path);
    }

    /// <summary>
    /// Returns the parent path or <code>null</code> if this is a root path.
    /// </summary>
    public FullPath? Parent {
      get {
        var parent = PathHelpers.GetParent(_path);
        if (parent == null)
          return null;
        return new FullPath(parent);
      }
    }

    /// <summary>
    /// Return the string representation of this full path.
    /// </summary>
    public string Value { get { return _path; } }

    /// <summary>
    /// Return the file name part of this full path.
    /// </summary>
    public string FileName { get { return PathHelpers.GetFileName(_path); } }

    /// <summary>
    /// Returns a full path instance as the combination of this full path with
    /// <paramref name="relativePath"/> appened at the end.
    /// </summary>
    public FullPath Combine(RelativePath relativePath) {
      return new FullPath(PathHelpers.CombinePaths(_path, relativePath.Value));
    }

    public static bool operator ==(FullPath x, FullPath y) {
      return x.Equals(y);
    }

    public static bool operator !=(FullPath x, FullPath y) {
      return !(x == y);
    }

    /// <summary>
    /// Returns true if <paramref name="other"/> is equal to this full path. Use
    /// FileSystem case insensitive comparer.
    /// </summary>
    public bool Equals(FullPath other) {
      return SystemPathComparer.Instance.StringComparer.Equals(_path, other._path);
    }

    /// <summary>
    /// Compares this full path with <paramref name="other"/>. Use FileSystem
    /// case insensitive comparer.
    /// </summary>
    public int CompareTo(FullPath other) {
      return SystemPathComparer.Instance.StringComparer.Compare(_path, other._path);
    }

    public override string ToString() {
      return _path;
    }

    /// <summary>
    /// Returns true if <paramref name="obj"/> is equal to this full path. Use
    /// FileSystem case insensitive comparer.
    /// </summary>
    public override bool Equals(object obj) {
      if (obj is FullPath)
        return Equals((FullPath)obj);
      return false;
    }

    /// <summary>
    /// Returns the hash code of this full path. Use FileSystem case insensitive
    /// comparer.
    /// </summary>
    public override int GetHashCode() {
      if (_path == null) {
        return 0;
      }
      return SystemPathComparer.Instance.StringComparer.GetHashCode(_path);
    }

    /// <summary>
    /// Returns true if this full path starts with <paramref name="x"/>
    /// </summary>
    public bool StartsWith(FullPath x) {
      return SystemPathComparer.Instance.StartsWith(_path, x._path);
    }

    /// <summary>
    /// Returns true if this full path contains <paramref name="component"/> as
    /// one of its component, i.e. parts between directory separator.
    /// </summary>
    public bool HasComponent(string component) {
      foreach (string currentComponent in PathHelpers.SplitPath(_path)) {
        if (currentComponent.Equals(component, StringComparison.CurrentCultureIgnoreCase))
          return true;
      }
      return false;
    }
  }

  public static class FullPathExtensions {
    /// <summary>
    /// Returns <code>true</code> if this path is <paramref name="childPath"/> or a
    /// parent of <paramref name="childPath"/>.
    /// </summary>
    public static bool ContainsPath(this FullPath path, FullPath childPath) {
      return PathHelpers.IsPrefix(path.Value, childPath.Value);
    }

    /// <summary>
    /// Enumerates all the paths from this <paramref name="path"/> to the root path,
    /// e.g. for the <code>"c:\foo\bar\blah.txt"</code> path, returns the sequence
    /// <code>"c:\foo\bar\blah.txt"</code>, <code>"c:\foo\bar"</code>,
    /// <code>"c:\foo"</code>, <code>"c:\"</code>.
    /// </summary>
    public static IEnumerable<FullPath> EnumeratePaths(this FullPath path) {
      for (FullPath? item = path; item != null; item = item.Value.Parent) {
        yield return item.Value;
      }
    }
  }
}
