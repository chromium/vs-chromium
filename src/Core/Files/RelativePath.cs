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
      _relativePath = relativePath;
    }

    /// <summary>
    /// Returns ture if this is the empty (default) relative path instance.
    /// </summary>
    public bool IsEmpty { get { return string.IsNullOrEmpty(_relativePath); } }

    /// <summary>
    /// Returns the string representation of the relative path.
    /// </summary>
    public string Value { get { return _relativePath ?? ""; } }

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
    /// Returns the parent path or null if this is a root path.
    /// </summary>
    public RelativePath Parent {
      get {
        var parent = PathHelpers.GetParent(_relativePath);
        return parent == null ? default(RelativePath) : new RelativePath(parent);
      }
    }

    public override string ToString() {
      return Value;
    }

    /// <summary>
    /// Returns a new relative path instance by appending <paramref name="name"/> to this instance.
    /// </summary>
    public RelativePath CreateChild(string name) {
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
