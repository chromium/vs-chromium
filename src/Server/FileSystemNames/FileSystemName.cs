// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public abstract class FileSystemName : IComparable<FileSystemName>, IEquatable<FileSystemName> {
    /// <summary>
    /// Returns the parent directory, or null for a <see cref="IsAbsoluteName"/> is true.
    /// </summary>
    public abstract DirectoryName Parent { get; }
    /// <summary>
    /// Returns the <see cref="RelativePathName"/>, which is empty only if <see
    /// cref="IsAbsoluteName"/> is true.
    /// Note: This operation does not perform any memory allocation.
    /// </summary>
    public abstract RelativePathName RelativePathName { get; }
    /// <summary>
    /// Returns true if this instances is an absolute directory name.
    /// </summary>
    public abstract bool IsAbsoluteName { get; }
    /// <summary>
    /// Returns the "name" component of this instances. For first level directory names,
    /// |name| contains the absolute directory name. For lower lever entries, "name" is
    /// is a relative name.
    /// </summary>
    public abstract string Name { get; }
    /// <summary>
    /// Return the <see cref="FullPathName"/> of this FileSystemName.
    /// Note: This operation typically performs a string concatenation.
    /// </summary>
    public abstract FullPathName FullPathName { get; }

    public override string ToString() {
      return FullPathName.FullName;
    }

    #region Comparison/Equality plumbing

    public int CompareTo(FileSystemName other) {
      return FileSystemNameComparer.Instance.Compare(this, other);
    }

    public bool Equals(FileSystemName other) {
      return FileSystemNameComparer.Instance.Equals(this, other);
    }

    public override int GetHashCode() {
      return FileSystemNameComparer.Instance.GetHashCode(this);
    }

    public override bool Equals(object other) {
      return Equals(other as FileSystemName);
    }

    #endregion
  }
}
