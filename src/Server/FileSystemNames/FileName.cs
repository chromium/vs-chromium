// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Buffers;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystemNames {
  /// <summary>
  /// Predicate on a relative path, see <see cref="FileName.TestRelativePath{TState}"/>.
  /// </summary>
  public delegate bool RelativePathPredicate<in TState>(ReadOnlySpan<char> relativePath, TState state);

  /// <summary>
  /// A value type representing a file name as a pair of parent <see cref="DirectoryName"/>
  /// and file name <see cref="String"/>.
  /// </summary>
  public struct FileName : IEquatable<FileName>, IComparable<FileName> {
    private readonly DirectoryName _parent;
    private readonly string _name;

    public FileName(DirectoryName parent, string name) {
      Invariants.CheckArgumentNotNull(parent, nameof(parent));
      Invariants.CheckArgumentNotNull(name, nameof(name), "File name is empty");
      Invariants.CheckArgument(PathHelpers.IsFileName(name), nameof(name), "File name contains one or more directory separator");
      _parent = parent;
      _name = name;
    }

    public DirectoryName Parent => _parent;

    public string Name => _name;

    public RelativePath RelativePath => BuildRelativePath();

    public FullPath BasePath {
      get {
        Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");
        // ReSharper disable once PossibleNullReferenceException
        return _parent.GetAbsolutePath();
      }
    }

    public FullPath FullPath {
      get {
        Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");
        // ReSharper disable once PossibleNullReferenceException
        return _parent.GetAbsolutePath().Combine(RelativePath);
      }
    }


    private static readonly string DirectorySeparatorString = new string(PathHelpers.DirectorySeparatorChar, 1);

    private RelativePath BuildRelativePath() {
      Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");

      // The relative path of directories is stored (see RelativeDirectoryName),
      // so this is a single string allocation.
      var parentPath = _parent.RelativePath;
      if (parentPath.IsEmpty)
        return new RelativePath(_name);
      return new RelativePath(string.Concat(parentPath.Value, DirectorySeparatorString, _name));
    }

    /// <summary>
    /// Evaluates <paramref name="predicate"/> on the relative path of this file name, built in a
    /// temporary buffer (no allocation, unlike <see cref="RelativePath"/>). For operations done on
    /// all the files, e.g. pattern matching.
    /// </summary>
    public bool TestRelativePath<TState>(RelativePathPredicate<TState> predicate, TState state) {
      Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");

      var parentPath = _parent.RelativePath.Value;
      if (parentPath.Length == 0)
        return predicate(_name.AsSpan(), state);

      var length = parentPath.Length + 1 + _name.Length;
      char[] rented = null;
      Span<char> buffer = length <= MaxStackPathLength
        ? stackalloc char[length]
        : (rented = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
      try {
        parentPath.AsSpan().CopyTo(buffer);
        buffer[parentPath.Length] = PathHelpers.DirectorySeparatorChar;
        _name.AsSpan().CopyTo(buffer.Slice(parentPath.Length + 1));
        return predicate(buffer, state);
      }
      finally {
        if (rented != null)
          ArrayPool<char>.Shared.Return(rented);
      }
    }

    private const int MaxStackPathLength = 512;

    /// <summary>
    /// Evaluates <paramref name="predicate"/> on the full path of this file name (same value as
    /// <see cref="FullPath"/>), built in a temporary buffer (no allocation).
    /// </summary>
    public bool TestFullPath<TState>(RelativePathPredicate<TState> predicate, TState state) {
      Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");

      var rootPath = _parent.GetAbsolutePath().Value;
      var parentPath = _parent.RelativePath.Value;
      // Same rules as PathHelpers.CombinePaths(root, relativePath)
      var last = rootPath.Length > 0 ? rootPath[rootPath.Length - 1] : PathHelpers.DirectorySeparatorChar;
      var rootSeparator = (last == System.IO.Path.DirectorySeparatorChar || last == System.IO.Path.AltDirectorySeparatorChar ||
                           last == System.IO.Path.VolumeSeparatorChar) ? 0 : 1;
      var length = rootPath.Length + rootSeparator + (parentPath.Length == 0 ? 0 : parentPath.Length + 1) + _name.Length;
      char[] rented = null;
      Span<char> buffer = length <= MaxStackPathLength
        ? stackalloc char[length]
        : (rented = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
      try {
        var index = 0;
        rootPath.AsSpan().CopyTo(buffer);
        index += rootPath.Length;
        if (rootSeparator == 1)
          buffer[index++] = PathHelpers.DirectorySeparatorChar;
        if (parentPath.Length > 0) {
          parentPath.AsSpan().CopyTo(buffer.Slice(index));
          index += parentPath.Length;
          buffer[index++] = PathHelpers.DirectorySeparatorChar;
        }
        _name.AsSpan().CopyTo(buffer.Slice(index));
        return predicate(buffer, state);
      }
      finally {
        if (rented != null)
          ArrayPool<char>.Shared.Return(rented);
      }
    }

    public override string ToString() {
      return $"{FullPath}, RelativePath={RelativePath}";
    }

    public override bool Equals(object obj) {
      if (obj is FileName) {
        return Equals((FileName) obj);
      }
      return false;
    }

    public override int GetHashCode() {
      if (_parent == null) {
        return 0;
      }
      return Core.Utility.HashCode.Combine(_parent.GetHashCode(), SystemPathComparer.GetHashCode(_name));
    }

    public bool Equals(FileName other) {
      return Equals(_parent, other._parent) &&
             SystemPathComparer.EqualsNames(_name, other._name);
    }

    public int CompareTo(FileName other) {
      var result = _parent.CompareTo(other._parent);
      if (result == 0) {
        result = SystemPathComparer.CompareNames(_name, other._name);
      }
      return result;
    }
  }
}
