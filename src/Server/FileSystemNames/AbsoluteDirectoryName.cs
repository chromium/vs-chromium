// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemNames {
  public class AbsoluteDirectoryName : DirectoryName {
    private readonly FullPath _path;

    public AbsoluteDirectoryName(FullPath path) {
      _path = path;
    }

    public override DirectoryName Parent { get { return null; } }
    public override RelativePath RelativePath { get { return default(RelativePath); } }
    public override FullPath FullPath { get { return _path; } }
  }
}