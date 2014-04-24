// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Server.FileSystemNames {
  public class FileSystemNameComparer : IComparer<FileSystemName>, IEqualityComparer<FileSystemName> {
    private static readonly FileSystemNameComparer _instance = new FileSystemNameComparer();

    public static FileSystemNameComparer Instance { get { return _instance; } }

    public int Compare(FileSystemName x, FileSystemName y) {
      var x1 = GetAbsolutePart(x);
      var y1 = GetAbsolutePart(y);
      var result = x1.FullPathName.CompareTo(y1.FullPathName);
      if (result == 0)
        result = x.RelativePathName.CompareTo(y.RelativePathName);
      return result;
    }

    public bool Equals(FileSystemName x, FileSystemName y) {
      if (x == null || y == null)
        return object.ReferenceEquals(x, y);
      return Compare(x, y) == 0;
    }

    public int GetHashCode(FileSystemName x) {
      var x1 = GetAbsolutePart(x);
      return CombineHashCodes(x1.FullPathName.GetHashCode(), x.RelativePathName.GetHashCode());
    }

    private FileSystemName GetAbsolutePart(FileSystemName name) {
      for (var x = name; x != null; x = x.Parent) {
        if (x.IsAbsoluteName)
          return x;
      }

      throw new InvalidOperationException("Invalid file system name (bug).");
    }

    private int CombineHashCodes(int h1, int h2) {
      unchecked {
        return (h1 << 5) + h1 ^ h2;
      }
    }
  }
}
