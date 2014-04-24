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
    private readonly string _name;
    private readonly string _relativeName;

    public RelativePathName(string relativeName, string name) {
      if (relativeName == null)
        throw new ArgumentNullException("relativeName");

      if (name == null)
        throw new ArgumentNullException("name");

      if (PathHelpers.IsAbsolutePath(relativeName))
        throw new ArgumentException("Path must be relative.", "relativeName");

      _relativeName = relativeName;
      _name = name;
    }

    public bool IsEmpty { get { return string.IsNullOrEmpty(_name); } }
    public string RelativeName { get { return _relativeName ?? ""; } }
    public string Name { get { return _name ?? ""; } }

    public override string ToString() {
      return this.RelativeName;
    }

    public RelativePathName CreateChild(string name) {
      return new RelativePathName(PathHelpers.PathCombine(this.RelativeName, name), name);
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
