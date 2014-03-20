// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public class DirectoryName : FileSystemName, IEquatable<DirectoryName> {
    public DirectoryName(DirectoryName parent, string name)
      : base(parent, name) {
    }

    public DirectoryName(DirectoryName parent, RelativePathName relativePathName)
      : base(parent, relativePathName) {
      if (parent == null)
        throw new ArgumentNullException("parent");
    }

    public bool Equals(DirectoryName other) {
      return base.Equals(other);
    }
  }
}
