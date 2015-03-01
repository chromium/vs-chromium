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
      if (object.ReferenceEquals(x, y))
        return 0;
      if (x == null)
        return -1;
      if (y == null)
        return 1;

      var xabs = x.GetAbsolutePath();
      var yabs = y.GetAbsolutePath();
      var result = xabs.CompareTo(yabs);
      if (result == 0)
        result = x.RelativePath.CompareTo(y.RelativePath);
      return result;
    }

    public bool Equals(FileSystemName x, FileSystemName y) {
      if (x == null || y == null)
        return object.ReferenceEquals(x, y);
      var xabs = x.GetAbsolutePath();
      var yabs = y.GetAbsolutePath();
      var result = xabs.Equals(yabs);
      if (result)
        result = x.RelativePath.Equals(y.RelativePath);
      return result;
    }

    public int GetHashCode(FileSystemName x) {
      var xabs = x.GetAbsolutePath();
      return HashCode.Combine(xabs.GetHashCode(), x.RelativePath.GetHashCode());
    }
  }
}
