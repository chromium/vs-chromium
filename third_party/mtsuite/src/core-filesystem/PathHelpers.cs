// Copyright 2026 Renaud Paquay All Rights Reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace mtsuite.CoreFileSystem {
  public static class PathHelpers {
    public static readonly string DirectorySeparatorString = Path.DirectorySeparatorChar.ToString();
    public static readonly string AltDirectorySeparatorString = Path.AltDirectorySeparatorChar.ToString();
    private static readonly string UncPathPrefix = @"\\";

    /// <summary>
    /// Returns the appropriate StringComparer for file and path names on the current operating system
    /// (Ordinal on Linux, OrdinalIgnoreCase on Windows and macOS).
    /// </summary>
    public static StringComparer FileNameComparer { get; } =
      OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Returns the appropriate StringComparison for file and path names on the current operating system
    /// (Ordinal on Linux, OrdinalIgnoreCase on Windows and macOS).
    /// </summary>
    public static StringComparison FileNameComparison { get; } =
      OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Return <code>true</code> if <paramref name="path"/> is an absolute path.
    /// </summary>
    public static bool IsPathAbsolute(string path) {
      return GetPathRootPrefixInfo(path).RootPrefixKind != RootPrefixKind.None;
    }

    /// <summary>
    /// Return <code>true</code> if <paramref name="path"/> is not an absolute path.
    /// </summary>
    public static bool IsPathRelative(string path) {
      return !IsPathAbsolute(path);
    }

    public static bool HasDirectorySeparators(string path) {
      return path.IndexOf(Path.DirectorySeparatorChar) >= 0;
    }

    public static bool HasAltDirectorySeparators(string path) {
      if (Path.AltDirectorySeparatorChar == Path.DirectorySeparatorChar) return false;
      return path.IndexOf(Path.AltDirectorySeparatorChar) >= 0;
    }

    private static bool IsDirectorySeparator(char ch) {
      return ch == '\\' || ch == '/';
    }

    private static int LastIndexOfDirectorySeparator(string path, int startIndex, int count) {
      for (int i = startIndex; i > startIndex - count && i >= 0; i--) {
        if (IsDirectorySeparator(path[i])) {
          return i;
        }
      }
      return -1;
    }

    /// <summary>
    /// Return the <paramref name="path"/> with its last component removed,
    /// or the <code>null</code> string if the path is a "root" path (e.g. "c:\").
    /// Throws an exception if <paramref name="path"/> is not an absolute path.
    /// </summary>
    public static string GetParent(string path) {
      if (!IsPathAbsolute(path))
        throw new ArgumentException("Path should be absolute", path);

      // Ignore '\' or '/' at the end of path
      var startIndex = path.Length - 1;
      var count = path.Length;
      if (startIndex >= 0 && IsDirectorySeparator(path[startIndex])) {
        startIndex--;
        count--;
      }
      if (startIndex < 0) return null;
      var lastIndex = LastIndexOfDirectorySeparator(path, startIndex, count);
      if (lastIndex < 0)
        return null;

      // Keep the terminating '\' to avoid returned invalid root path (e.g. 'c:')
      var result = path.Substring(0, lastIndex + 1);
      if (result == UncPathPrefix)
        return null;
      return result;
    }

    /// <summary>
    /// Return the last component of the <paramref name="path"/> or <code>null</code>
    /// if the path is a root path (e.g. "c:\").
    /// Throws an exception if <paramref name="path"/> is not an absolute path.
    /// </summary>
    public static string GetName(string path) {
      if (!IsPathAbsolute(path))
        throw new ArgumentException("Path should be absolute", path);

      var startIndex = path.Length - 1;
      var count = path.Length;
      if (startIndex >= 0 && IsDirectorySeparator(path[startIndex])) {
        startIndex--;
        count--;
      }
      if (startIndex < 0) return null;
      var lastIndex = LastIndexOfDirectorySeparator(path, startIndex, count);
      if (lastIndex < 0)
        return null;

      // Check the remaining prefix is not a root path prefix (e.g. "\\").
      var prefix = path.Substring(0, lastIndex + 1);
      if (prefix == UncPathPrefix)
        return null;

      return path.Substring(lastIndex + 1, count - lastIndex - 1);
    }

    /// <summary>
    /// Returns a valid absolute path given a valid current directory and a path
    /// from unvalidated user input. Throws an exception if <paramref name="userPath"/> cannot
    /// be validated into a valid absolute path.
    /// </summary>
    public static string NormalizeUserInputPath(string currentDirectory, string userPath) {
      if (!IsPathAbsolute(currentDirectory))
        throw new ArgumentException("Current directory path should be absolute", currentDirectory);

      var path = userPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
      if (!IsPathAbsolute(path)) {
        // Special case: path is of the form "x:": Add a terminating "\" to make it valid
        if (path.Length == 2 && char.IsLetter(path[0]) && path[1] == Path.VolumeSeparatorChar) {
          path = path + Path.DirectorySeparatorChar;
        }
        else {
          currentDirectory = currentDirectory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
          path = CombinePaths(currentDirectory, path);
        }
      }

      path = NormalizePath(path);
      return path;
    }

    /// <summary>
    /// Return <paramref name="path"/> ensuring <paramref name="path"/> has a trailing directory separator.
    /// </summary>
    public static string AppendTrailingSeparator(string path) {
      if (string.IsNullOrEmpty(path))
        throw new ArgumentNullException("path");

      path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
      if (path[path.Length - 1] == Path.DirectorySeparatorChar)
        return path;

      return path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Return <paramref name="path"/> ensuring if the (optional) trailing
    /// directory separator is removed.
    /// </summary>
    public static string RemoveTrailingSeparator(string path) {
      if (string.IsNullOrEmpty(path))
        throw new ArgumentNullException("path");

      path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
      if (path[path.Length - 1] == Path.DirectorySeparatorChar)
        return path;

      return path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Combine absolute source path <paramref name="path1"/> with the relative
    /// path <paramref name="path2"/>. This is nothing more than a helper method
    /// to add a directory separator characters between two paths if needed.
    /// </summary>
    public static string CombinePaths(string path1, string path2) {
      if (string.IsNullOrEmpty(path2))
        throw new ArgumentNullException("path2");

      path1 = path1 ?? "";
      path1 = path1.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
      path2 = path2.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

      if (path1 == "") {
        if (!IsPathAbsolute(path2))
          throw new ArgumentException("Path should be absolute", path2);
        return path2;
      }

      if (!IsPathAbsolute(path1))
        throw new ArgumentException("Path should be absolute", path1);
      if (IsPathAbsolute(path2))
        throw new ArgumentException("Path should be relative", path2);

      var hasTrailingSeparator = path1[path1.Length - 1] == Path.DirectorySeparatorChar;
      var hasLeadingSeparator = path2[0] == Path.DirectorySeparatorChar;
      if (hasTrailingSeparator) {
        if (hasLeadingSeparator)
          return path1 + path2.Substring(1);
        else
          return path1 + path2;
      } else {
        if (hasLeadingSeparator)
          return path1 + path2;
        else {
          return path1 + DirectorySeparatorString + path2;
        }
      }
    }

    /// <summary>
    /// Normalize <paramref name="path"/> into an absolute path, by removing any occurrence
    /// of <code>.</code> or <code>..</code> inside the path.
    /// </summary>
    public static string NormalizePath(string path) {
      if (string.IsNullOrEmpty(path))
        throw new ArgumentNullException("path");

      path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

      var sb = new StringBuilder();
      var lexer = new PathLexer(path);

      var prefixInfo = GetPathRootPrefixInfo(path);
      if (prefixInfo.Length > 0) {
        lexer.Skip(prefixInfo.Length);
      }

      var parts = lexer.Split(Path.DirectorySeparatorChar).ToList();
      for (var i = 0; i < parts.Count;) {
        if (parts[i] == ".") {
          parts.RemoveAt(i);
          continue;
        }

        if (parts[i] == "..") {
          if (i > 0) {
            parts.RemoveAt(i);
            parts.RemoveAt(i - 1);
            i--;
            continue;
          }
          parts.RemoveAt(i);
          continue;
        }

        i++;
      }

      for (var i = 0; i < parts.Count; i++) {
        if (i > 0)
          sb.Append(Path.DirectorySeparatorChar);
        sb.Append(parts[i]);
      }
      sb.Insert(0, path.Substring(0, prefixInfo.Length));
      return sb.ToString();
    }

    /// <summary>
    /// Return <code>true</code> if <paramref name="path"/> is an absolute path.
    /// </summary>
    public static PathRootPrefixInfo GetPathRootPrefixInfo(string path) {
      if (string.IsNullOrEmpty(path))
        return default(PathRootPrefixInfo);

      // Device format: 'X:\' prefix
      int localDiskPrefix = GetDiskRootPrefix(path, 0);
      if (localDiskPrefix > 0)
        return new PathRootPrefixInfo(localDiskPrefix, RootPrefixKind.DiskPath);

      // UNC Format: '\\' prefix
      if (path.Length >= 2 && path[0] == '\\' && path[1] == '\\')
        return new PathRootPrefixInfo(2, RootPrefixKind.UncPath);

      // Unix Format: '/' prefix
      if (path.Length >= 1 && path[0] == '/')
        return new PathRootPrefixInfo(1, RootPrefixKind.UnixPath);

      return default(PathRootPrefixInfo);
    }

    private static int GetDiskRootPrefix(string path, int index) {
      if (path.Length >= index + 3 && char.IsLetter(path[index + 0]) && path[index + 1] == ':' && path[index + 2] == '\\') {
        return 3;
      }
      return 0;
    }

    public struct PathRootPrefixInfo {
      private readonly int _length;
      private readonly RootPrefixKind _rootPrefixKind;

      public PathRootPrefixInfo(int length, RootPrefixKind rootPrefixKind) {
        _length = length;
        _rootPrefixKind = rootPrefixKind;
      }

      public int Length {
        get { return _length; }
      }

      public RootPrefixKind RootPrefixKind {
        get { return _rootPrefixKind; }
      }
    }

    public enum RootPrefixKind {
      None,
      DiskPath,
      UncPath,
      UnixPath,
    }

    private class PathLexer {
      private readonly string _path;
      private int _index;

      public PathLexer(string path) {
        _path = path;
      }

      public void Skip(int length) {
        _index += length;
      }

      public IEnumerable<string> Split(char sep) {
        return _path.Substring(_index).Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);
      }
    }
  }
}