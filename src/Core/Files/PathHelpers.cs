// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Core.Files {
  public static class PathHelpers {
    private const int MaxPath = 260;
    private static readonly string DirectorySeparatorString = new string(Path.DirectorySeparatorChar, 1);
    private static readonly char[] DirectorySeparatorArray = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    /// <summary>
    /// Combines two paths into a single path. More efficient than <see
    /// cref="System.IO.Path.Combine(string, string)"/>, because we don't box
    /// "Path.DirectorySeparatorChar"
    /// </summary>
    public static string CombinePaths(string path1, string path2) {
      if (path1 == null)
        throw new ArgumentNullException("path1");
      if (path2 == null)
        throw new ArgumentNullException("path2");

      if (path2.Length == 0)
        return path1;

      if (path1.Length == 0)
        return path2;

      var c = path1[path1.Length - 1];
      if (c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar && c != Path.VolumeSeparatorChar) {
        return path1 + DirectorySeparatorString + path2;
      }
      return path1 + path2;
    }

    /// <summary>
    /// Returns true if the <paramref name="path"/> argument represents an
    /// absolute path. More efficient than <see
    /// cref="System.IO.Path.IsPathRooted"/>, but is not 100% correct, so this
    /// method should only be used in assertion code.
    /// </summary>
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

    /// <summary>
    /// Returns true if <paramref name="path"/> is a simple file name and
    /// extension, i.e. if <paramref name="path"/> does not contain any
    /// directory separator.
    /// </summary>
    public static bool IsFileName(string path) {
      return path.IndexOfAny(DirectorySeparatorArray) < 0;
    }

    /// <summary>
    /// Return the <paramref name="path"/> argument where all directory
    /// separators are replaced with the Posix separator ("/")
    /// </summary>
    public static string ToPosix(string path) {
      return path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Returns the parent directory of <paramref name="path"/>, or null if
    /// <paramref name="path"/> is a top level directory.
    /// </summary>
    public static string GetParent(string path) {
      return Path.GetDirectoryName(path);
    }

    /// <summary>
    /// Returns <code>true</code> if <paramref name="path"/> is too long.
    /// </summary>
    public static bool IsPathTooLong(string path) {
      path = path ?? "";
      return path.Length >= MaxPath;
    }

    /// <summary>
    /// Returns <code>true</code> if <paramref name="path"/> is too long.
    /// </summary>
    public static bool IsValidBclPath(string path) {
      if (IsPathTooLong(path))
        return false;

      try {
        // This call checks path contains valid characters only, etc.
        Path.GetDirectoryName(path);
        return true;
      } catch (Exception) {
        return false;
      }
    }
  }
}
