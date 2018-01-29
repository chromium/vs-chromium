// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystemNames {
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


    /// <summary>
    /// We use a <see cref="ThreadStaticAttribute"/> field to GC memory allocations
    /// during heavy parallel task execution.
    /// </summary>
    [ThreadStatic]
    private static StringBuilder _dangerousThreadStaticStringBuilder;

    private RelativePath BuildRelativePath() {
      Invariants.CheckOperation(_parent != null, "File name is invalid (uninitialized value type)");

      // Aquire the StringBuilder from thread static variable.
      if (_dangerousThreadStaticStringBuilder == null) {
        _dangerousThreadStaticStringBuilder = new StringBuilder(128);
      }
      var sb = _dangerousThreadStaticStringBuilder;
      sb.Clear();

      // Build the relative path
      DirectoryName.BuildRelativePath(sb, _parent);
      if (sb.Length > 0) {
        sb.Append(PathHelpers.DirectorySeparatorChar);
      }
      sb.Append(_name);
      return new RelativePath(sb.ToString());
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
      return HashCode.Combine(_parent.GetHashCode(), SystemPathComparer.GetHashCode(_name));
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
