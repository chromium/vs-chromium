// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemNames {
  /// <summary>
  /// Abstraction over a file or directory name in the file system. We use a
  /// common pattern of having File and Directory names represented relative to
  /// their parent. However, this particular implementation allows the very
  /// first part of the name (the <see cref="IsAbsoluteName"/> part) to be a
  /// directory name with multiple levels (e.g. @"d:\foo\bar"). Also, for
  /// performance reason, i.e. to keep string memory allocation in check,
  /// instances store internally either the full path name, or the relative path
  /// from the parent containing a full path name.
  ///
  /// For example, to represent the file name "d:\foo\bar\baz\blah.txt" relative
  /// to the "d:\foo\bar" absolute path, we have this hierarchy of
  /// instances:
  /// FileName
  ///   RelativePath = "baz\blah.txt"
  ///   Parent => RelativeDirectoryName
  ///     RelativePath = "baz"
  ///     Parent => AbsoluteDirectory
  ///       FullPath = "d:\foo\bar"
  ///       Parent = null
  /// </summary>
  public abstract class FileSystemName : IComparable<FileSystemName>, IEquatable<FileSystemName> {
    /// <summary>
    /// Returns the parent directory, or null if <see cref="IsAbsoluteName"/> is true.
    /// </summary>
    public abstract DirectoryName Parent { get; }

    /// <summary>
    /// Returns a valid <see cref="RelativePath"/> if <see
    /// cref="IsAbsoluteName"/> is false, or the empty <see
    /// cref="RelativePath"/> otherwise.
    /// Note: Perf: This operation does not perform any memory allocation.
    /// </summary>
    public abstract RelativePath RelativePath { get; }

    /// <summary>
    /// Return the <see cref="FullPath"/> of this instance.
    /// Note: Perf: This operation performs a string concatenation, unless <see
    /// cref="IsAbsoluteName"/> is true.
    /// </summary>
    public abstract FullPath FullPath { get; }

    /// <summary>
    /// Returns true if this instance is an absolute directory name, false
    /// otherwise. <see cref="IsAbsoluteName"/> implies <see cref="Parent"/> is
    /// null and <see cref="RelativePath"/> is empty.
    /// </summary>
    public bool IsAbsoluteName { get { return Parent == null; } }

    /// <summary>
    /// Return the <see cref="FullPath"/> of the parent name.
    /// </summary>
    protected FullPath GetParentAbsolutePathName() {
      for (var currentParent = Parent; currentParent != null; currentParent = currentParent.Parent) {
        if (currentParent.IsAbsoluteName)
          return currentParent.FullPath;
      }
      return ThrowNoParent();
    }

    private FullPath ThrowNoParent() {
      throw new InvalidOperationException("Name does not have a parent with an absolute path.");
    }

    public override string ToString() {
      return FullPath.Value;
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
