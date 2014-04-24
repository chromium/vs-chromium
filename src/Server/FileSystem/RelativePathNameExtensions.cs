// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.FileNames;
using VsChromium.Core.Win32.Files;

namespace VsChromium.Server.FileSystem {
  public static class RelativePathNameExtensions {
    public static void GetFileSystemEntries(FullPathName rootPath, RelativePathName path, out RelativePathName[] directories, out RelativePathName[] files) {
      IList<string> shortDirectoryNames;
      IList<string> shortFileNames;
      NativeFile.GetDirectoryEntries(PathHelpers.PathCombine(rootPath.FullName, path.RelativeName), out shortDirectoryNames, out shortFileNames);
      directories = CreateChildren(path, shortDirectoryNames);
      files = CreateChildren(path, shortFileNames);
    }

    private static RelativePathName[] CreateChildren(RelativePathName path, IList<string> names) {
      var result = new RelativePathName[names.Count];
      // Note: Use "for" loop to avoid memory allocation.
      for (var i = 0; i < names.Count; i++) {
        result[i] = path.CreateChild(names[i]);
      }
      return result;
    }
  }
}
