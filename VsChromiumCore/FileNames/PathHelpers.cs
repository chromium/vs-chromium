// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;

namespace VsChromiumCore.FileNames {
  public static class PathHelpers {
    private static readonly string _directorySeparatorString = new string(Path.DirectorySeparatorChar, 1);

    /// <summary>
    /// More efficient than "Path.Combine", because we don't box "Path.DirectorySeparatorChar"
    /// </summary>
    public static string PathCombine(string path1, string path2) {
      if (path2.Length == 0)
        return path1;

      if (path1.Length == 0)
        return path2;

      var c = path1[path1.Length - 1];
      if (c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar && c != Path.VolumeSeparatorChar) {
        return path1 + _directorySeparatorString + path2;
      }
      return path1 + path2;
    }

    public static bool IsAbsolutePath(string path) {
      // Quick & dirty check, but faster than "Path.IsPathRooted".
      var l = path.Length;

      if (l > 1 && path[1] == Path.VolumeSeparatorChar) {
        return true;
      } else if (l > 0) {
        var c = path[0];
        if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
          return true;
      }

      return false;
    }
  }
}
