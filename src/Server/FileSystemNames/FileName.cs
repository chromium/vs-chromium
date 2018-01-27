// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystemNames {
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
    public RelativePath RelativePath => _parent.RelativePath.CreateChild(_name);
    public FullPath FullPath => _parent.GetAbsolutePath().Combine(RelativePath);
    public string Name => _name;

    public override bool Equals(object obj) {
      if (obj is FileName) {
        return Equals((FileName) obj);
      }
      return false;
    }

    public override int GetHashCode() {
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
