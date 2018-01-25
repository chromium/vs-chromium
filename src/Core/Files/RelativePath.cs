// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Wrapper around a relative path name (file or directory). For performance
  /// reasons (i.e. to decrease string allocations), we also internally store
  /// the "filename" part of the path (i.e. the part of the path after the last
  /// directory separator).
  /// </summary>
  public struct RelativePath : IEquatable<RelativePath>, IComparable<RelativePath> {
    private readonly string _relativePath;
    private readonly string _filename;

    public static readonly RelativePath Empty = default(RelativePath);

    /// <summary>
    /// Creates a <see cref="RelativePath"/> instance from a relative path
    /// string. Note this constructor is less efficient than the constructor
    /// with two arguments, as this constuctor needs to extract the file name
    /// from the relative path name.
    /// </summary>
    public RelativePath(string relativePath)
      : this(relativePath, ExtractFileName(relativePath)) {
    }

    /// <summary>
    /// Creates a <see cref="RelativePath"/> instance from a relative path
    /// string (e.g. "foo\\bar\\blah.txt") and the corresponding file name (e.g.
    /// "blah.txt"). <paramref name="relativePath"/> must be end with <paramref
    /// name="filename"/>.
    /// </summary>
    public RelativePath(string relativePath, string filename) {
      Invariants.CheckArgumentNotNull(relativePath, nameof(relativePath));
      Invariants.CheckArgumentNotNull(filename, nameof(filename));
      Invariants.CheckArgument(!PathHelpers.IsAbsolutePath(relativePath), nameof(relativePath), "Path must be relative.");
      Invariants.CheckArgument(PathHelpers.IsFileName(filename), nameof(filename), "Path must be a simple file name + extension.");
      Invariants.CheckArgument(relativePath.Length >= filename.Length, nameof(relativePath), "Relative path must contain file name");
      Invariants.CheckArgument(relativePath.EndsWith(filename, StringComparison.Ordinal), nameof(relativePath), "Relative path must end with file name");

      _relativePath = relativePath;
      _filename = filename;
    }

    private static string ExtractFileName(string relativePath) {
      Invariants.CheckArgumentNotNull(relativePath, nameof(relativePath));
      if (PathHelpers.IsFileName(relativePath))
        return relativePath;
      return PathHelpers.GetFileName(relativePath);
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
    /// Returns the string reprensentation of the last part (i.e. the file or
    /// directory name) of the relative path.
    /// </summary>
    public string FileName { get { return _filename ?? ""; } }

    /// <summary>
    /// Return the file extension (including the dot).
    /// </summary>
    public string Extension {
      get {
        var name = _filename ?? "";
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
      return new RelativePath(PathHelpers.CombinePaths(Value, name), name);
    }

    #region Comparison/Equality plumbing

    public int CompareTo(RelativePath other) {
      return SystemPathComparer.Instance.StringComparer.Compare(Value, other.Value);
    }

    public bool Equals(RelativePath other) {
      return SystemPathComparer.Instance.StringComparer.Equals(Value, other.Value);
    }

    public override int GetHashCode() {
      return SystemPathComparer.Instance.StringComparer.GetHashCode(Value);
    }

    public override bool Equals(object other) {
      if (other is RelativePath)
        return Equals((RelativePath)other);
      return false;
    }

    #endregion
  }
}
