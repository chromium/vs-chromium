// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public abstract class FileSystemName : IComparable<FileSystemName>, IEquatable<FileSystemName> {
    private readonly string _name;
    private readonly DirectoryName _parent;
    private readonly RelativePathName _relativePathName;

    protected FileSystemName(DirectoryName parent, string name) {
      if (name == null)
        throw new ArgumentNullException("name");

      _parent = parent;
      _name = name;
      _relativePathName = CreateRelativePathName(parent, name);
    }

    protected FileSystemName(DirectoryName parent, RelativePathName relativePathName) {
      _parent = parent;
      _name = relativePathName.Name;
      _relativePathName = relativePathName;
    }

    public DirectoryName Parent { get { return _parent; } }

    public RelativePathName RelativePathName { get { return _relativePathName; } }

    public bool IsAbsoluteName { get { return _relativePathName.RelativeName == ""; } }

    /// <summary>
    /// Returns the "name" component of this instances. For first level directory names,
    /// |name| contains the absolute directory name. For lower lever entries, "name" is
    /// is a relative name.
    /// </summary>
    public string Name { get { return _name; } }

    /// <summary>
    /// Return "true" for the root node of the file system name tree, i.e. it is the node returned from <c>FileSystemNameFactory.Root</c>
    /// The Root node has no parent and an empty name.
    /// </summary>
    public bool IsRoot { get { return _parent == null; } }

    private static RelativePathName CreateRelativePathName(DirectoryName parent, string name) {
      // parent can be null for root entry
      if (parent == null)
        return default(RelativePathName);

      if (parent.IsRoot) {
        if (!PathHelpers.IsAbsolutePath(name))
          throw new ArgumentException("Path must be absolute for first level of directory name.", "name");
        return new RelativePathName("", "");
      }

      return parent._relativePathName.CreateChild(name);
    }

    public string GetFullName() {
      if (IsRoot)
        return _name;

      for (var parent = this; parent != null; parent = parent._parent) {
        if (parent.IsAbsoluteName)
          return PathHelpers.PathCombine(parent._name, _relativePathName.RelativeName);
      }
      throw new InvalidOperationException("FileSystemName entry does not have a parent with an absolute path.");
    }

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
