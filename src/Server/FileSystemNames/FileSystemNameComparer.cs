// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystemNames {
  public class FileSystemNameComparer : IComparer<FileSystemName>, IEqualityComparer<FileSystemName> {
    private static readonly FileSystemNameComparer _instance = new FileSystemNameComparer();

    public static FileSystemNameComparer Instance { get { return _instance; } }

    public int Compare(FileSystemName x, FileSystemName y) {
      if (x == null) {
        if (y == null)
          return 0;
        else
          return -1;
      } else {
        if (y == null)
          return 1;
      }

      var x1 = GetAbsolutePart(x);
      var y1 = GetAbsolutePart(y);
      var result = x1.FullPath.CompareTo(y1.FullPath);
      if (result == 0)
        result = x.RelativePath.CompareTo(y.RelativePath);
      else {
        // If the absolute parts are not equal, we may be in a case where we
        // have 2 distinct representations of the same canonical path name.
        // Instead of implementing a complex algorithm, just compare the full
        // paths (at the cost of a string concatenation).
        return x.FullPath.CompareTo(y.FullPath);
      }
      return result;
    }

    public bool Equals(FileSystemName x, FileSystemName y) {
      if (x == null || y == null)
        return object.ReferenceEquals(x, y);
      var x1 = GetAbsolutePart(x);
      var y1 = GetAbsolutePart(y);
      var result = x1.FullPath.Equals(y1.FullPath);
      if (result)
        result = x.RelativePath.Equals(y.RelativePath);
      else {
        // If the absolute parts are not equal, we may be in a case where we
        // have 2 distinct representations of the same canonical path name.
        // Instead of implementing a complex algorithm, just compare the full
        // paths (at the cost of a string concatenation).
        return x.FullPath.Equals(y.FullPath);
      }
      return result;
    }

    public int GetHashCode(FileSystemName x) {
      var x1 = GetAbsolutePart(x);
      return HashCode.Combine(x1.FullPath.GetHashCode(), x.RelativePath.GetHashCode());
    }

    private FileSystemName GetAbsolutePart(FileSystemName name) {
      for (var x = name; x != null; x = x.Parent) {
        if (x.IsAbsoluteName)
          return x;
      }

      throw new InvalidOperationException("Invalid file system name (bug).");
    }
  }
}
