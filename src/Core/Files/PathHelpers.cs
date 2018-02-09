// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VsChromium.Core.Files {
  public static class PathHelpers {
    private const int MaxPath = 260;
    private static readonly string DirectorySeparatorString = new string(Path.DirectorySeparatorChar, 1);
    private static readonly char[] DirectorySeparatorArray = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
    private static readonly string NetworkSharePrefix = new string(Path.DirectorySeparatorChar, 2);
    public static char DirectorySeparatorChar = Path.DirectorySeparatorChar;

    /// <summary>
    /// Combines two paths into a single path. More efficient than <see
    /// cref="System.IO.Path.Combine(string, string)"/>, because we don't box
    /// "Path.DirectorySeparatorChar"
    /// </summary>
    public static string CombinePaths(string path1, string path2) {
      if (string.IsNullOrEmpty(path1))
        return path2;

      if (string.IsNullOrEmpty(path2))
        return path1;

      var c = path1[path1.Length - 1];
      if (c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar && c != Path.VolumeSeparatorChar) {
        return path1 + DirectorySeparatorString + path2;
      }
      return path1 + path2;
    }

    /// <summary>
    /// Return <code>true</code> if <paramref name="prefix"/> is a path prefix
    /// of <paramref name="path"/>
    /// </summary>
    public static bool IsPrefix(string prefix, string path) {
      var result = SystemPathComparer.Instance.IndexOf(path, prefix, 0, path.Length);
      if (result < 0)
        return false;

      // Check that "path" contains a directory separator
      if (prefix.Last() == Path.DirectorySeparatorChar)
        return true;
      if (path.Length <= prefix.Length)
        return true;
      return path[prefix.Length] == Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Splits a path into its components, e.g c:\foo\bar into "c:", "foo" and "bar".
    /// </summary>
    public static IEnumerable<string> SplitPath(string path) {
      if (string.IsNullOrEmpty(path)) {
        yield break;
      }

      int start = 0;
      StringBuilder sb = new StringBuilder();
      if (path.StartsWith(NetworkSharePrefix)) {
        start = NetworkSharePrefix.Length;
        sb.Append(NetworkSharePrefix);
      }
      for (var index = start; index < path.Length; index++) {
        char ch = path[index];
        if (ch == Path.DirectorySeparatorChar) {
          yield return sb.ToString();
          sb.Clear();
        }
        else {
          sb.Append(ch);
        }
      }
      if (sb.Length > 0) {
        yield return sb.ToString();
      }
    }

    /// <summary>
    /// Splits a path into an absolute path plus a relative path, using
    /// <paramref name="prefix"/> as the path prefix.
    /// </summary>
    public static SplitPath SplitPrefix(string path, string prefix) {
      if (string.IsNullOrEmpty(path))
        throw new ArgumentException();
      if (string.IsNullOrEmpty(prefix))
        throw new ArgumentException();

      var prefixEnd = prefix.Length - 1;
      while (prefixEnd >= 0) {
        if (path[prefixEnd] != Path.DirectorySeparatorChar)
          break;
        prefixEnd--;
      }

      var suffixStart = prefix.Length;
      while (suffixStart < path.Length) {
        if (path[suffixStart] != Path.DirectorySeparatorChar)
          break;
        suffixStart++;
      }

      var root = path.Substring(0, prefixEnd + 1);
      var relativePath = path.Substring(suffixStart);
      return new SplitPath(root, relativePath);
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
      var result = Path.GetDirectoryName(path);
      if (result == "") {
        return null;
      }
      return result;
    }

    /// <summary>
    /// Returns <code>true</code> if <paramref name="path"/> is too long.
    /// </summary>
    public static bool IsPathTooLong(string path) {
      path = path ?? "";
      return path.Length >= MaxPath;
    }

    /// <summary>
    /// Returns <code>true</code> if <paramref name="parentPath"/> combined with
    /// <param name="relativePath"></param> is too long.
    /// </summary>
    public static bool IsPathTooLong(string parentPath, string relativePath) {
      if (string.IsNullOrEmpty(parentPath))
        throw new ArgumentException();

      if (string.IsNullOrEmpty(relativePath))
        return IsPathTooLong(parentPath);

      return (parentPath.Length + 1 + relativePath.Length) >= MaxPath;
    }

    /// <summary>
    /// Returns <code>true</code> if <paramref name="path"/> is too long.
    /// </summary>
    public static bool IsValidBclPath(string path) {
      if (IsPathTooLong(path))
        return false;

      try {
        // This call checks path contains valid characters only, etc.
        // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
        Path.GetDirectoryName(path);
        return true;
      }
      catch (Exception) {
        return false;
      }
    }

    public static string GetFileName(string path) {
      if (string.IsNullOrEmpty(path)) {
        return path;
      }
      int length = path.Length;
      int index = length;
      while (--index >= 0) {
        char ch = path[index];
        if (ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar || ch == Path.VolumeSeparatorChar)
          return path.Substring(index + 1, length - index - 1);
      }
      return path;
    }

    public static string GetExtension(string path) {
      return Path.GetExtension(path) ?? "";
    }
  }

  /// <summary>
  /// Simple abstraction over a path split into its root (absolute) part and a
  /// relative suffix, which may be empty.
  /// </summary>
  public struct SplitPath {
    private readonly string _root;
    private readonly string _suffix;

    public SplitPath(string root, string suffix) {
      if (string.IsNullOrEmpty(root))
        throw new ArgumentException();
      if (suffix == null)
        throw new ArgumentException();
      _root = root;
      _suffix = suffix;
    }

    /// <summary>
    /// Root of the path, never empty or null. Does *not* end with path
    /// separator.
    /// </summary>
    public string Root {
      get { return _root; }
    }

    /// <summary>
    /// Suffix of the path, after <see cref="Root"/>. Can be empty string (but
    /// not null). Does *not* start with a path separator.
    /// </summary>
    public string Suffix {
      get { return _suffix; }
    }
  }
}
