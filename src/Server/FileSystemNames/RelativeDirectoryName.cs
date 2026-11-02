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
    /// <summary>
    /// Stored (one string per directory, a few MB for 40,000 directories) so
    /// that the relative path of files is built with a single concatenation.
    /// </summary>
    private readonly RelativePath _relativePath;

    public RelativeDirectoryName(DirectoryName parent, string name) {
      Invariants.CheckArgumentNotNull(parent, nameof(parent));
      Invariants.CheckArgumentNotNull(name, nameof(name), "Directory name is empty");
      Invariants.CheckArgument(PathHelpers.IsFileName(name), nameof(name), "Directory name contains one or more directory separator");
      _parent = parent;
      _name = name;
      _hashCode = HashCode.Combine(_parent.GetHashCode(), SystemPathComparer.GetHashCode(_name));
      var parentPath = parent.RelativePath;
      _relativePath = new RelativePath(parentPath.IsEmpty ? name : parentPath.Value + PathHelpers.DirectorySeparatorChar + name);
    }

    public override DirectoryName Parent => _parent;
    public override RelativePath RelativePath => _relativePath;
    public override FullPath FullPath => _parent.GetAbsolutePath().Combine(RelativePath);
    public override string Name => _name;

    public override int GetHashCode() {
      return _hashCode;
    }
  }
}
