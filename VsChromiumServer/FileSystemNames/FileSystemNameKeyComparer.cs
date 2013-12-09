// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromiumServer.FileSystemNames {
  public class FileSystemNameKeyComparer : IEqualityComparer<FileSystemNameKey> {
    public bool Equals(FileSystemNameKey x, FileSystemNameKey y) {
      return x.Equals(y);
    }

    public int GetHashCode(FileSystemNameKey obj) {
      return obj.GetHashCode();
    }
  }
}
