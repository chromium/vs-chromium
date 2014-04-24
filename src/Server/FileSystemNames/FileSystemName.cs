// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public abstract class FileSystemName : IComparable<FileSystemName>, IEquatable<FileSystemName> {
    public abstract DirectoryName Parent { get; }
    public abstract RelativePathName RelativePathName { get; }
    public abstract bool IsAbsoluteName { get; }

    /// <summary>
    /// Returns the "name" component of this instances. For first level directory names,
    /// |name| contains the absolute directory name. For lower lever entries, "name" is
    /// is a relative name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Return "true" for the root node of the file system name tree, i.e. it is the node returned from <c>FileSystemNameFactory.Root</c>
    /// The Root node has no parent and an empty name.
    /// </summary>
    public abstract bool IsRoot { get; }

    public abstract string GetFullName();

    public override string ToString() {
      return GetFullName();
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
