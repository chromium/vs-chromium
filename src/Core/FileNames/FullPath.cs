// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.FileNames {
  /// <summary>
  /// Wraps a string representing the full path of a file or directory.
  /// </summary>
  public struct FullPath : IEquatable<FullPath>, IComparable<FullPath> {
    private readonly string _path;

    public FullPath(string path) {
      if (!PathHelpers.IsAbsolutePath(path))
        ThrowInvalidPath(path);
      _path = path;
    }

    private static void ThrowInvalidPath(string path) {
      throw new ArgumentException(string.Format("Path must be absolute: \"{0}\".", path), "path");
    }

    /// <summary>
    /// Returns the parent path or null if this is a root path.
    /// </summary>
    public FullPath Parent {
      get {
        var parent = Directory.GetParent(_path);
        return parent == null ? default(FullPath) : new FullPath(parent.FullName);
      }
    }

    /// <summary>
    /// Return the string representation of this full path.
    /// </summary>
    public string FullName { get { return _path; } }

    /// <summary>
    /// Return the file name part of this full path.
    /// </summary>
    public string Name { get { return Path.GetFileName(_path); } }

    /// <summary>
    /// Returns a full path instance as the combination of this full path with
    /// <paramref name="name"/> appened at the end.
    /// </summary>
    public FullPath Combine(string name) {
      return new FullPath(PathHelpers.CombinePaths(_path, name));
    }

    /// <summary>
    /// Returns true if the file corresponding to the full path exists on disk.
    /// </summary>
    public bool FileExists { get { return File.Exists(_path); } }

    public static bool operator ==(FullPath x, FullPath y) {
      return x.Equals(y);
    }

    public static bool operator !=(FullPath x, FullPath y) {
      return !(x == y);
    }

    /// <summary>
    /// Returns true if <paramref name="other"/> is equal to this full path. Use
    /// FileSystem case insensitive comparer.
    /// </summary>
    public bool Equals(FullPath other) {
      return SystemPathComparer.Instance.Comparer.Equals(_path, other._path);
    }

    /// <summary>
    /// Compares this full path with <paramref name="other"/>. Use FileSystem
    /// case insensitive comparer.
    /// </summary>
    public int CompareTo(FullPath other) {
      return SystemPathComparer.Instance.Comparer.Compare(_path, other._path);
    }

    public override string ToString() {
      return _path;
    }

    /// <summary>
    /// Returns true if <paramref name="obj"/> is equal to this full path. Use
    /// FileSystem case insensitive comparer.
    /// </summary>
    public override bool Equals(object obj) {
      if (obj is FullPath)
        return Equals((FullPath)obj);
      return false;
    }

    /// <summary>
    /// Returns the hash code of this full path. Use FileSystem case insensitive
    /// comparer.
    /// </summary>
    public override int GetHashCode() {
      return SystemPathComparer.Instance.Comparer.GetHashCode(_path);
    }

    /// <summary>
    /// Returns true if this full path starts with <paramref name="x"/>
    /// </summary>
    public bool StartsWith(FullPath x) {
      return _path.StartsWith(x._path, SystemPathComparer.Instance.Comparison);
    }

    /// <summary>
    /// Returns true if this full path contains <paramref name="component"/> as
    /// one of its component, i.e. parts between directory separator.
    /// </summary>
    public bool HasComponent(string component) {
      foreach (string currentComponent in _path.Split(Path.DirectorySeparatorChar)) {
        if (currentComponent.Equals(component, StringComparison.CurrentCultureIgnoreCase))
          return true;
      }
      return false;
    }

    /// <summary>
    /// Returns the enumeration of the parent full path of this full path.
    /// </summary>
    public IEnumerable<FullPath> EnumerateParents() {
      for (var parent = Parent; parent != default(FullPath); parent = parent.Parent) {
        yield return parent;
      }
    }
  }
}
