// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystemNames {
  public class FileName : IEquatable<FileName>, IComparable<FileName> {
    private readonly DirectoryName _parent;
    private readonly string _name;

    public FileName(DirectoryName parent, string name) {
      Invariants.CheckArgumentNotNull(parent, nameof(parent));
      Invariants.CheckArgumentNotNull(name, nameof(name), "File name is empty");
      Invariants.CheckArgument(PathHelpers.IsFileName(name), nameof(name), "File name contains directory separator");
      _parent = parent;
      _name = name;
    }

    public DirectoryName Parent { get { return _parent; } }
    public RelativePath RelativePath { get { return _parent.RelativePath.CreateChild(_name); } }
    public FullPath FullPath { get { return _parent.GetAbsolutePath().Combine(RelativePath); } }
    public string Name { get { return _name; } }

    public override bool Equals(object obj) {
      if (obj is FileName) {
        return Equals((FileName) obj);
      }
      return false;
    }

    public override int GetHashCode() {
      return HashCode.Combine(_parent.GetHashCode(), SystemPathComparer.Instance.StringComparer.GetHashCode(_name));
    }

    public bool Equals(FileName other) {
      if (other == null) {
        return false;
      }
      return Equals(_parent, other._parent) &&
             SystemPathComparer.EqualsNames(_name, other._name);
    }

    public int CompareTo(FileName other) {
      if (other == null) {
        return 1;
      }

      int result = _parent.CompareTo(other._parent);
      if (result == 0) {
        result = SystemPathComparer.CompareNames(_name, other._name);
      }
      return result;
    }
  }
}
