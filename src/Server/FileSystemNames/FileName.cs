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

    public FullPath FullPath {
      get {
        Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");
        // ReSharper disable once PossibleNullReferenceException
        return _parent.GetAbsolutePath().Combine(RelativePath);
      }
    }


    private static readonly char DirectorySeparatorChar = PathHelpers.DirectorySeparatorChar;

    /// <summary>
    /// The length of the relative path of this file name.
    /// </summary>
    private int RelativePathLength {
      get {
        var parentLength = _parent.RelativePathLength;
        return parentLength == 0 ? _name.Length : parentLength + 1 + _name.Length;
      }
    }

    /// <summary>
    /// Copies the relative path (<see cref="RelativePathLength"/> characters) to <paramref
    /// name="destination"/>.
    /// </summary>
    private void CopyRelativePathTo(Span<char> destination) {
      var parentLength = _parent.RelativePathLength;
      if (parentLength == 0) {
        _name.AsSpan().CopyTo(destination);
        return;
      }
      _parent.CopyRelativePathTo(destination);
      destination[parentLength] = DirectorySeparatorChar;
      _name.AsSpan().CopyTo(destination.Slice(parentLength + 1));
    }

    private RelativePath BuildRelativePath() {
      Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");

      // Note: The relative path of directories is not stored (see RelativeDirectoryName), it is
      // copied from the names of the parents: a single string allocation.
      if (_parent.RelativePathLength == 0)
        return new RelativePath(_name);
      return new RelativePath(string.Create(RelativePathLength, this, static (span, fileName) => fileName.CopyRelativePathTo(span)));
    }

    /// <summary>
    /// Evaluates <paramref name="predicate"/> on the relative path of this file name, built in a
    /// temporary buffer (no allocation, unlike <see cref="RelativePath"/>). For operations done on
    /// all the files, e.g. pattern matching.
    /// </summary>
    public bool TestRelativePath<TState>(RelativePathPredicate<TState> predicate, TState state) {
      Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");

      if (_parent.RelativePathLength == 0)
        return predicate(_name.AsSpan(), state);

      var length = RelativePathLength;
      char[] rented = null;
      Span<char> buffer = length <= DirectoryName.MaxStackPathLength
        ? stackalloc char[length]
        : (rented = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
      try {
        CopyRelativePathTo(buffer);
        return predicate(buffer, state);
      }
      finally {
        if (rented != null)
          ArrayPool<char>.Shared.Return(rented);
      }
    }

    /// <summary>
    /// Evaluates <paramref name="predicate"/> on the full path of this file name (same value as
    /// <see cref="FullPath"/>), built in a temporary buffer (no allocation).
    /// </summary>
    public bool TestFullPath<TState>(RelativePathPredicate<TState> predicate, TState state) {
      Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");

      var rootPath = _parent.GetAbsolutePath().Value;
      var relativeLength = RelativePathLength;
      // Same rules as PathHelpers.CombinePaths(root, relativePath)
      var last = rootPath.Length > 0 ? rootPath[rootPath.Length - 1] : DirectorySeparatorChar;
      var rootSeparator = (last == System.IO.Path.DirectorySeparatorChar || last == System.IO.Path.AltDirectorySeparatorChar ||
                           last == System.IO.Path.VolumeSeparatorChar) ? 0 : 1;
      var length = rootPath.Length + rootSeparator + relativeLength;
      char[] rented = null;
      Span<char> buffer = length <= DirectoryName.MaxStackPathLength
        ? stackalloc char[length]
        : (rented = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
      try {
        rootPath.AsSpan().CopyTo(buffer);
        var index = rootPath.Length;
        if (rootSeparator == 1)
          buffer[index++] = DirectorySeparatorChar;
        CopyRelativePathTo(buffer.Slice(index, relativeLength));
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
