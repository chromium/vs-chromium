// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Wrapper around a relative path name (file or directory).
  /// </summary>
  public struct RelativePath : IEquatable<RelativePath>, IComparable<RelativePath> {
    private readonly string _relativePath;

    public static readonly RelativePath Empty = default(RelativePath);

    /// <summary>
    /// Creates a <see cref="RelativePath"/> instance from a relative
    /// path string (e.g. "foo\\bar\\blah.txt").
    /// </summary>
    public RelativePath(string relativePath) {
      Invariants.CheckArgumentNotNull(relativePath, nameof(relativePath));
      Invariants.CheckArgument(!PathHelpers.IsAbsolutePath(relativePath), nameof(relativePath), "Path must be relative.");
      // Empty string is the same as the empty relative path
      _relativePath = relativePath == "" ? null : relativePath;
    }

    /// <summary>
    /// Returns ture if this is the empty (default) relative path instance.
    /// </summary>
    public bool IsEmpty => _relativePath == null;

    /// <summary>
    /// Returns the string representation of the relative path.
    /// </summary>
    public string Value => _relativePath ?? "";

    /// <summary>
    /// Return the file extension (including the dot).
    /// </summary>
    public string Extension {
      get {
        var name = _relativePath ?? "";
        var index = name.LastIndexOf('.');
        if (index < 0)
          return "";
        return name.Substring(index);
      }
    }

    /// <summary>
    /// Returns the parent path or <code>null</code> if this is the empty relative path
    /// </summary>
    public RelativePath? Parent {
      get {
        if (_relativePath == null) {
          return null;
        }
        var parent = PathHelpers.GetParent(_relativePath);
        return parent == null ? Empty : new RelativePath(parent);
      }
    }

    public override string ToString() {
      return Value;
    }

    /// <summary>
    /// Returns a new relative path instance by appending <paramref name="name"/> to this instance.
    /// </summary>
    public RelativePath CreateChild(string name) {
      if (_relativePath == null) {
        return new RelativePath(name);
      }
      return new RelativePath(PathHelpers.CombinePaths(_relativePath, name));
    }

    #region Comparison/Equality plumbing

    public int CompareTo(RelativePath other) {
      return SystemPathComparer.Instance.StringComparer.Compare(_relativePath, other._relativePath);
    }

    public bool Equals(RelativePath other) {
      return SystemPathComparer.Instance.StringComparer.Equals(_relativePath, other._relativePath);
    }

    public override int GetHashCode() {
      if (_relativePath == null) {
        return 0;
      }
      return SystemPathComparer.Instance.StringComparer.GetHashCode(_relativePath);
    }

    public override bool Equals(object other) {
      if (other is RelativePath)
        return Equals((RelativePath)other);
      return false;
    }

    #endregion
  }
}
