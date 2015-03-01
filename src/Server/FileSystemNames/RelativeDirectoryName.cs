// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemNames {
  public class RelativeDirectoryName : DirectoryName {
    private readonly DirectoryName _parent;
    private readonly RelativePath _relativePath;

    public RelativeDirectoryName(DirectoryName parent, RelativePath relativePath) {
      if (parent == null)
        throw new ArgumentNullException("parent");
      if (relativePath.IsEmpty)
        throw new ArgumentException("Relative path is empty", "relativePath");
      _parent = parent;
      _relativePath = relativePath;
    }

    public override DirectoryName Parent { get { return _parent; } }
    public override RelativePath RelativePath { get { return _relativePath; } }
    public override FullPath FullPath { get { return _parent.GetAbsolutePath().Combine(_relativePath); } }
  }
}