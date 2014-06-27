// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.FileNames {
  /// <summary>
  /// Wrapper around a relative path name (file or directory).
  /// For performance reasons, we also keep the "name" part of the path
  /// (i.e. the part of the path after the last directory separator).
  /// </summary>
  public struct RelativePathName : IEquatable<RelativePathName>, IComparable<RelativePathName> {
    private readonly string _relativeName;
    private readonly string _filename;

    /// <summary>
    /// Creates a <see cref="RelativePathName"/> instance from a simple file
    /// name + extension. The file name may not contains any directory
    /// separators.
    /// </summary>
    public RelativePathName(string filename)
      : this(filename, filename) {
    }

    /// <summary>
    /// Creates a <see cref="RelativePathName"/> instance from a relative path
    /// name (e.g. foo\\bar\\blah.txt) and the corresponding file name (e.g.
    /// blah.txt). <paramref name="relativeName"/> must be end with <paramref
    /// name="filename"/>.
    /// </summary>
    public RelativePathName(string relativeName, string filename) {
      if (relativeName == null)
        throw new ArgumentNullException("relativeName");

      if (filename == null)
        throw new ArgumentNullException("filename");

      if (PathHelpers.IsAbsolutePath(relativeName))
        throw new ArgumentException("Path must be relative.", "relativeName");

      if (!PathHelpers.IsFileName(filename))
        throw new ArgumentException("Path must be a simple file name + extension.", "filename");

      if (relativeName.Length < filename.Length)
        throw new ArgumentException("Relative path must contain file name", "relativeName");

      if (string.Compare(relativeName, relativeName.Length - filename.Length, filename, 0, filename.Length, StringComparison.Ordinal) != 0)
        throw new ArgumentException("Relative path must end with file name", "relativeName");

      _relativeName = relativeName;
      _filename = filename;
    }

    /// <summary>
    /// Returns ture if this is the empty (default) relative path instance.
    /// </summary>
    public bool IsEmpty { get { return string.IsNullOrEmpty(_filename); } }

    /// <summary>
    /// Returns the string representation of the relative path name.
    /// </summary>
    public string RelativeName { get { return _relativeName ?? ""; } }

    /// <summary>
    /// Returns the string reprensentation of the last part (i.e. the file or
    /// directory name) of the relative path name.
    /// </summary>
    public string FileName { get { return _filename ?? ""; } }

    public override string ToString() {
      return this.RelativeName;
    }

    /// <summary>
    /// Returns a new relative path instance by appending <paramref name="name"/> to this instance.
    /// </summary>
    public RelativePathName CreateChild(string name) {
      return new RelativePathName(PathHelpers.CombinePaths(this.RelativeName, name), name);
    }

    #region Comparison/Equality plumbing

    public int CompareTo(RelativePathName other) {
      return SystemPathComparer.Instance.Comparer.Compare(this.RelativeName, other.RelativeName);
    }

    public bool Equals(RelativePathName other) {
      return SystemPathComparer.Instance.Comparer.Equals(this.RelativeName, other.RelativeName);
    }

    public override int GetHashCode() {
      return SystemPathComparer.Instance.Comparer.GetHashCode(this.RelativeName);
    }

    public override bool Equals(object other) {
      if (other is RelativePathName)
        return Equals((RelativePathName)other);
      return false;
    }

    #endregion
  }
}
