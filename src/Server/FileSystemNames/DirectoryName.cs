// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Buffers;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;

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
  /// instances (relative paths are computed from the names, not stored):
  /// FileName
  ///   RelativePath = "baz\blah.txt"
  ///   Parent => RelativeDirectoryName
  ///     RelativePath = "baz"
  ///     Parent => AbsoluteDirectory
  ///       FullPath = "d:\foo\bar"
  ///       Parent = null
  /// </summary>
  public abstract class DirectoryName : IComparable<DirectoryName>, IEquatable<DirectoryName> {
    /// <summary>
    /// Returns the parent directory, or null if <see cref="IsAbsoluteName"/> is true.
    /// </summary>
    public abstract DirectoryName Parent { get; }

    /// <summary>
    /// Returns a valid <see cref="RelativePath"/> if <see
    /// cref="IsAbsoluteName"/> is false, or the empty <see
    /// cref="RelativePath"/> otherwise.
    /// </summary>
    public abstract RelativePath RelativePath { get; }

    /// <summary>
    /// The length of <see cref="RelativePath"/> (0 if <see cref="IsAbsoluteName"/> is true),
    /// without building it.
    /// </summary>
    public abstract int RelativePathLength { get; }

    /// <summary>
    /// Copies the relative path (<see cref="RelativePathLength"/> characters) to
    /// <paramref name="destination"/>, without allocating.
    /// </summary>
    public void CopyRelativePathTo(Span<char> destination) {
      var end = RelativePathLength;
      for (var current = this; !current.IsAbsoluteName; current = current.Parent) {
        var name = current.Name;
        var start = end - name.Length;
        name.AsSpan().CopyTo(destination.Slice(start));
        if (start > 0)
          destination[start - 1] = PathHelpers.DirectorySeparatorChar;
        end = start - 1;
      }
    }

    /// <summary>
    /// Builds the relative path string (a single allocation).
    /// </summary>
    protected string BuildRelativePath() {
      return string.Create(RelativePathLength, this, static (span, directory) => directory.CopyRelativePathTo(span));
    }

    /// <summary>
    /// Evaluates <paramref name="predicate"/> on the relative path of this directory, built in a
    /// temporary buffer (no allocation, unlike <see cref="RelativePath"/>).
    /// </summary>
    public bool TestRelativePath<TState>(RelativePathPredicate<TState> predicate, TState state) {
      var length = RelativePathLength;
      char[] rented = null;
      Span<char> buffer = length <= MaxStackPathLength
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
    /// Paths longer than this are built in a rented buffer instead of on the stack.
    /// </summary>
    public const int MaxStackPathLength = 512;

    /// <summary>
    /// Return the <see cref="FullPath"/> of this instance.
    /// Note: Perf: This operation performs a string concatenation, unless <see
    /// cref="IsAbsoluteName"/> is true.
    /// </summary>
    public abstract FullPath FullPath { get; }

    /// <summary>
    /// The directory name (i.e. the name after the last directory separator)
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Returns true if this instance is an absolute directory name, false
    /// otherwise. <see cref="IsAbsoluteName"/> implies <see cref="Parent"/> is
    /// null and <see cref="RelativePath"/> is empty.
    /// </summary>
    public bool IsAbsoluteName => Parent == null;

    /// <summary>
    /// Return the <see cref="FullPath"/> of the parent name.
    /// </summary>
    public FullPath GetAbsolutePath() {
      for (var current = this; current != null; current = current.Parent) {
        if (current.IsAbsoluteName)
          return current.FullPath;
      }
      throw Invariants.Fail("Name does not have a parent with an absolute path.");
    }

    public override string ToString() {
      return FullPath.Value;
    }

    #region Comparison/Equality plumbing
    public int CompareTo(DirectoryName other) {
      return DirectoryNameComparer.Instance.Compare(this, other);
    }

    public bool Equals(DirectoryName other) {
      return DirectoryNameComparer.Instance.Equals(this, other);
    }

    public override int GetHashCode() {
      return DirectoryNameComparer.Instance.GetHashCode(this);
    }

    public override bool Equals(object other) {
      return Equals(other as DirectoryName);
    }
    #endregion
  }
}
