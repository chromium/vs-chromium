// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemNames {
  public class AbsoluteDirectoryName : DirectoryName {
    private readonly FullPath _path;
    private readonly int _hashCode;

    public AbsoluteDirectoryName(FullPath path) {
      _path = path;
      _hashCode = path.GetHashCode();
    }

    public override DirectoryName Parent => null;
    public override RelativePath RelativePath => default(RelativePath);
    public override FullPath FullPath => _path;
    public override string Name => _path.FileName;

    public override int GetHashCode() {
      return _hashCode;
    }
  }
}