// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public class RelativeDirectoryName : DirectoryName {
    private readonly DirectoryName _parent;
    private readonly RelativePathName _relativePathName;

    public RelativeDirectoryName(DirectoryName parent, RelativePathName relativePathName) {
      if (parent == null)
        throw new ArgumentNullException("parent");
      if (relativePathName.IsEmpty)
        throw new ArgumentException("Relative path is empty", "relativePathName");
      _parent = parent;
      _relativePathName = relativePathName;
    }

    public override DirectoryName Parent { get { return _parent; } }
    public override RelativePathName RelativePathName { get { return _relativePathName; } }
    public override FullPathName FullPathName { get { return GetParentAbsolutePathName().Combine(_relativePathName.RelativeName); } }
  }
}