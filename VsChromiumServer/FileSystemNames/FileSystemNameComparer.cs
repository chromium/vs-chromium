// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromiumCore.FileNames;

namespace VsChromiumServer.FileSystemNames {
  public class FileSystemNameComparer : IComparer<FileSystemName>, IEqualityComparer<FileSystemName> {
    private static readonly FileSystemNameComparer _instance = new FileSystemNameComparer();

    public static FileSystemNameComparer Instance {
      get {
        return _instance;
      }
    }

    public int Compare(FileSystemName x, FileSystemName y) {
      if (object.ReferenceEquals(x, y))
        return 0;

      if (x == null && y == null)
        return 0;
      if (x == null)
        return -1;
      if (y == null)
        return 1;

      var x1 = GetAbsolutePart(x);
      var y1 = GetAbsolutePart(y);

      if (x1.IsRoot && y1.IsRoot)
        return 0;
      if (x1.IsRoot)
        return -1;
      if (y1.IsRoot)
        return 1;

      var result = 0;
      if (!object.ReferenceEquals(x1, y1))
        result = SystemPathComparer.Instance.Comparer.Compare(x1.Name, y1.Name);
      if (result == 0)
        result = SystemPathComparer.Instance.Comparer.Compare(x.RelativePathName.RelativeName, y.RelativePathName.RelativeName);
      return result;
    }

    public bool Equals(FileSystemName x, FileSystemName y) {
      return Compare(x, y) == 0;
    }

    public int GetHashCode(FileSystemName x) {
      if (x == null)
        return 0;

      var x1 = GetAbsolutePart(x);
      if (x1.IsRoot)
        return 1;

      unchecked {
        if (x.IsAbsoluteName)
          return SystemPathComparer.Instance.Comparer.GetHashCode(x1.Name);
        else
          return CombineHashCodes(
              SystemPathComparer.Instance.Comparer.GetHashCode(x1.Name),
              SystemPathComparer.Instance.Comparer.GetHashCode(x.RelativePathName.RelativeName));
      }
    }

    private FileSystemName GetAbsolutePart(FileSystemName name) {
      for (var x = name; x != null; x = x.Parent) {
        if (x.IsRoot || x.IsAbsoluteName)
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
