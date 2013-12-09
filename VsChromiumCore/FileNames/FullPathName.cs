// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;

namespace VsChromiumCore.FileNames {
  /// <summary>
  /// Wraps a string representing the full path of a file or directory.
  /// </summary>
  public struct FullPathName : IEquatable<FullPathName> {
    private readonly string _path;

    public FullPathName(string path) {
      if (!PathHelpers.IsAbsolutePath(path))
        throw new ArgumentException("Path must be absolute.", "path");
      this._path = path;
    }

    public FullPathName Parent {
      get {
        var parent = Directory.GetParent(this._path);
        return parent == null ? default(FullPathName) : new FullPathName(parent.FullName);
      }
    }

    public string FullName {
      get {
        return this._path;
      }
    }

    public string Name {
      get {
        return Path.GetFileName(this._path);
      }
    }

    public FullPathName Combine(string name) {
      return new FullPathName(PathHelpers.PathCombine(this._path, name));
    }

    public bool FileExists {
      get {
        return File.Exists(this._path);
      }
    }

    public bool DirectoryExists {
      get {
        return Directory.Exists(this._path);
      }
    }

    public static bool operator ==(FullPathName x, FullPathName y) {
      return x.Equals(y);
    }

    public static bool operator !=(FullPathName x, FullPathName y) {
      return !(x == y);
    }

    public bool Equals(FullPathName other) {
      return SystemPathComparer.Instance.Comparer.Equals(this._path, other._path);
    }

    public override string ToString() {
      return this._path;
    }

    public override bool Equals(object obj) {
      if (obj is FullPathName)
        return Equals((FullPathName)obj);
      return false;
    }

    public override int GetHashCode() {
      return SystemPathComparer.Instance.Comparer.GetHashCode(this._path);
    }

    public bool StartsWith(FullPathName x) {
      return this._path.StartsWith(x._path, SystemPathComparer.Instance.Comparison);
    }

    public IEnumerable<FullPathName> EnumerateParents() {
      for (var parent = this.Parent; parent != default(FullPathName); parent = parent.Parent) {
        yield return parent;
      }
    }
  }
}
