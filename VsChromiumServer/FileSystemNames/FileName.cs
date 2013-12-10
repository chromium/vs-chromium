// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.FileNames;

namespace VsChromiumServer.FileSystemNames {
  public class FileName : FileSystemName, IEquatable<FileName> {
    public FileName(DirectoryName parent, string name)
      : base(parent, name) {
      if (parent == null)
        throw new ArgumentNullException("parent");
    }

    public FileName(DirectoryName parent, RelativePathName relativePathName)
      : base(parent, relativePathName) {
      if (parent == null)
        throw new ArgumentNullException("parent");
    }

    public bool Equals(FileName other) {
      return base.Equals(other);
    }
  }
}
