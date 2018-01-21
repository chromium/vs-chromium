// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemNames {
  public class RelativeDirectoryName : DirectoryName {
    private readonly DirectoryName _parent;
    private readonly string _name;

    public RelativeDirectoryName(DirectoryName parent, string name) {
      if (parent == null)
        throw new ArgumentNullException("parent");
      if (string.IsNullOrEmpty(name))
        throw new ArgumentException("Directory name is empty", "name");
      if (!PathHelpers.IsFileName(name))
        throw new ArgumentException("Directory name contains directory separator", "name");
      _parent = parent;
      _name = name;
    }

    public override DirectoryName Parent { get { return _parent; } }
    public override RelativePath RelativePath { get { return BuildRelativePath(this); } }
    public override FullPath FullPath { get { return _parent.GetAbsolutePath().Combine(RelativePath); } }
    public override string Name { get { return _name; } }
  }
}