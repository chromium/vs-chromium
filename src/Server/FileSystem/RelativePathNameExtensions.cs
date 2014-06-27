// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.FileNames;
using VsChromium.Core.Win32.Files;

namespace VsChromium.Server.FileSystem {
  public static class RelativePathNameExtensions {
    public static void GetFileSystemEntries(FullPathName rootPath, RelativePathName path, out IList<string> directories, out IList<string> files) {
      NativeFile.GetDirectoryEntries(PathHelpers.CombinePaths(rootPath.FullName, path.RelativeName), out directories, out files);
    }
  }
}
