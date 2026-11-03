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
    /// Length of the relative path (the path itself is not stored: one string per directory
    /// was ~150 MB for 640,000 directories). Relative paths are built on demand, or copied into
    /// temporary buffers (see <see cref="DirectoryName.CopyRelativePathTo"/>).
    /// </summary>
    private readonly int _relativePathLength;

    public RelativeDirectoryName(DirectoryName parent, string name) {
      Invariants.CheckArgumentNotNull(parent, nameof(parent));
      Invariants.CheckArgumentNotNull(name, nameof(name), "Directory name is empty");
      Invariants.CheckArgument(PathHelpers.IsFileName(name), nameof(name), "Directory name contains one or more directory separator");
      _parent = parent;
      _name = name;
      _hashCode = HashCode.Combine(_parent.GetHashCode(), SystemPathComparer.GetHashCode(_name));
      var parentLength = parent.RelativePathLength;
      _relativePathLength = parentLength == 0 ? name.Length : parentLength + 1 + name.Length;
    }

    public override DirectoryName Parent => _parent;
    public override int RelativePathLength => _relativePathLength;
    public override RelativePath RelativePath => new RelativePath(BuildRelativePath());
    public override FullPath FullPath => _parent.GetAbsolutePath().Combine(RelativePath);
    public override string Name => _name;

    public override int GetHashCode() {
      return _hashCode;
    }
  }
}
