// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystemNames {
  public class RelativeDirectoryName : DirectoryName {
    private readonly DirectoryName _parent;
    private readonly string _name;
    private readonly int _hashCode;

    public RelativeDirectoryName(DirectoryName parent, string name) {
      Invariants.CheckArgumentNotNull(parent, nameof(parent));
      Invariants.CheckArgumentNotNull(name, nameof(name), "Directory name is empty");
      Invariants.CheckArgument(PathHelpers.IsFileName(name), nameof(name), "Directory name contains one or more directory separator");
      _parent = parent;
      _name = name;
      _hashCode = HashCode.Combine(_parent.GetHashCode(), SystemPathComparer.GetHashCode(_name));
    }

    public override DirectoryName Parent => _parent;
    public override RelativePath RelativePath => BuildRelativePath(this);
    public override FullPath FullPath => _parent.GetAbsolutePath().Combine(RelativePath);
    public override string Name => _name;

    public override int GetHashCode() {
      return _hashCode;
    }
  }
}