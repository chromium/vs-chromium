// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Text;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Platform-specific path operations and comparison rules (Windows, Linux, macOS).
  /// </summary>
  public interface IPlatformPathHelper {
    char DirectorySeparatorChar { get; }
    StringComparer FileNameComparer { get; }
    StringComparison FileNameComparison { get; }
    IPathComparer PathComparer { get; }

    string CombinePaths(string path1, string path2);
    bool IsPrefix(string prefix, string path);
    IEnumerable<string> SplitPath(string path);
    SplitPath SplitPrefix(string path, string prefix);
    bool IsAbsolutePath(string path);
    bool IsFileName(string path);
    string GetParent(string path);
    string GetFileName(string path);
    string GetExtension(string path);
    bool IsValidBclPath(string path);
  }

  /// <summary>
  /// Windows implementation ('\' and '/' separators, drive/UNC roots, case-insensitive).
  /// </summary>
  public sealed class WindowsPathHelper : IPlatformPathHelper {
    public char DirectorySeparatorChar => '\\';
    public StringComparer FileNameComparer => StringComparer.OrdinalIgnoreCase;
    public StringComparison FileNameComparison => StringComparison.OrdinalIgnoreCase;
    public IPathComparer PathComparer => PathComparerRegistry.CaseInsensitive;

    public string CombinePaths(string path1, string path2) {
      if (string.IsNullOrEmpty(path1))
        return path2;
      if (string.IsNullOrEmpty(path2))
        return path1;

      var c = path1[path1.Length - 1];
      if (!IsSep(c) && c != ':')
        return IsSep(path2[0]) ? path1 + path2 : path1 + "\\" + path2;
      return IsSep(c) && IsSep(path2[0]) ? path1 + path2.Substring(1) : path1 + path2;
    }

    public bool IsPrefix(string prefix, string path) {
      if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(path) || path.Length < prefix.Length)
        return false;
      if (PathComparer.IndexOf(path, prefix, 0, prefix.Length) != 0)
        return false;
      return IsSep(prefix[prefix.Length - 1]) || path.Length == prefix.Length || IsSep(path[prefix.Length]);
    }

    public IEnumerable<string> SplitPath(string path) {
      if (string.IsNullOrEmpty(path))
        yield break;

      var start = 0;
      var sb = new StringBuilder();
      if (path.Length >= 2 && IsSep(path[0]) && IsSep(path[1])) {
        start = 2;
        sb.Append(@"\\");
      }
      for (var i = start; i < path.Length; i++) {
        var ch = path[i];
        if (IsSep(ch)) {
          if (sb.Length > 0) {
            yield return sb.ToString();
            sb.Clear();
          }
        } else {
          sb.Append(ch);
        }
      }
      if (sb.Length > 0)
        yield return sb.ToString();
    }

    public SplitPath SplitPrefix(string path, string prefix) {
      if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(prefix))
        throw new ArgumentException();

      var prefixEnd = prefix.Length - 1;
      while (prefixEnd >= 0 && IsSep(path[prefixEnd]))
        prefixEnd--;

      var suffixStart = prefix.Length;
      while (suffixStart < path.Length && IsSep(path[suffixStart]))
        suffixStart++;

      return new SplitPath(path.Substring(0, prefixEnd + 1), path.Substring(suffixStart));
    }

    public bool IsAbsolutePath(string path) {
      if (string.IsNullOrEmpty(path))
        return false;
      return (path.Length >= 3 && IsDriveLetter(path[0]) && path[1] == ':' && IsSep(path[2])) ||
             (path.Length >= 2 && IsSep(path[0]) && IsSep(path[1]));
    }

    public bool IsFileName(string path) {
      if (path == null)
        throw new ArgumentNullException(nameof(path));
      return path.AsSpan().IndexOfAny('\\', '/') < 0;
    }

    public string GetParent(string path) {
      if (string.IsNullOrEmpty(path))
        return null;

      var minRoot = GetRootLength(path);
      var end = path.Length - 1;
      while (end >= minRoot && IsSep(path[end]))
        end--;
      if (end < minRoot)
        return null;

      var sep = end;
      while (sep >= minRoot && !IsSep(path[sep]))
        sep--;
      if (sep < minRoot)
        return minRoot == 3 ? path.Substring(0, 2) + "\\" : null;

      while (sep > minRoot && IsSep(path[sep - 1]))
        sep--;
      return path.Substring(0, sep);
    }

    public string GetFileName(string path) {
      if (string.IsNullOrEmpty(path))
        return path;
      for (var i = path.Length - 1; i >= 0; i--) {
        var ch = path[i];
        if (IsSep(ch) || ch == ':')
          return path.Substring(i + 1);
      }
      return path;
    }

    public string GetExtension(string path) {
      if (string.IsNullOrEmpty(path))
        return "";
      for (var i = path.Length - 1; i >= 0; i--) {
        var ch = path[i];
        if (ch == '.')
          return i != path.Length - 1 ? path.Substring(i) : "";
        if (IsSep(ch) || ch == ':')
          break;
      }
      return "";
    }

    public bool IsValidBclPath(string path) {
      if (string.IsNullOrEmpty(path))
        return false;
      for (var i = 0; i < path.Length; i++) {
        var ch = path[i];
        if (ch < 32 || ch == '"' || ch == '<' || ch == '>' || ch == '|')
          return false;
      }
      return true;
    }

    private static bool IsSep(char ch) => ch == '\\' || ch == '/';
    private static bool IsDriveLetter(char ch) => (uint)((ch | 0x20) - 'a') <= ('z' - 'a');

    private static int GetRootLength(string path) {
      if (path.Length >= 3 && IsDriveLetter(path[0]) && path[1] == ':' && IsSep(path[2]))
        return 3;
      if (path.Length >= 2 && IsSep(path[0]) && IsSep(path[1])) {
        var i = 2;
        while (i < path.Length && IsSep(path[i])) i++;
        while (i < path.Length && !IsSep(path[i])) i++;
        while (i < path.Length && IsSep(path[i])) i++;
        while (i < path.Length && !IsSep(path[i])) i++;
        return i;
      }
      return 0;
    }
  }

  /// <summary>
  /// Shared POSIX base ('/' separator only, '/' root) for <see cref="LinuxPathHelper"/> and <see cref="MacPathHelper"/>.
  /// </summary>
  public abstract class PosixPathHelper : IPlatformPathHelper {
    protected PosixPathHelper(bool caseSensitive) {
      FileNameComparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
      FileNameComparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
      PathComparer = caseSensitive ? PathComparerRegistry.CaseSensitive : PathComparerRegistry.CaseInsensitive;
    }

    public char DirectorySeparatorChar => '/';
    public StringComparer FileNameComparer { get; }
    public StringComparison FileNameComparison { get; }
    public IPathComparer PathComparer { get; }

    public string CombinePaths(string path1, string path2) {
      if (string.IsNullOrEmpty(path1))
        return path2;
      if (string.IsNullOrEmpty(path2))
        return path1;

      var c1 = path1[path1.Length - 1] == '/';
      var c2 = path2[0] == '/';
      if (c1 && c2)
        return path1 + path2.Substring(1);
      if (c1 || c2)
        return path1 + path2;
      return path1 + "/" + path2;
    }

    public bool IsPrefix(string prefix, string path) {
      if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(path) || path.Length < prefix.Length)
        return false;
      if (PathComparer.IndexOf(path, prefix, 0, prefix.Length) != 0)
        return false;
      return prefix[prefix.Length - 1] == '/' || path.Length == prefix.Length || path[prefix.Length] == '/';
    }

    public IEnumerable<string> SplitPath(string path) {
      if (string.IsNullOrEmpty(path))
        yield break;

      var sb = new StringBuilder();
      for (var i = 0; i < path.Length; i++) {
        var ch = path[i];
        if (ch == '/') {
          if (sb.Length > 0) {
            yield return sb.ToString();
            sb.Clear();
          }
        } else {
          sb.Append(ch);
        }
      }
      if (sb.Length > 0)
        yield return sb.ToString();
    }

    public SplitPath SplitPrefix(string path, string prefix) {
      if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(prefix))
        throw new ArgumentException();

      var prefixEnd = prefix.Length - 1;
      while (prefixEnd >= 0 && path[prefixEnd] == '/')
        prefixEnd--;

      var suffixStart = prefix.Length;
      while (suffixStart < path.Length && path[suffixStart] == '/')
        suffixStart++;

      var root = prefixEnd < 0 ? "/" : path.Substring(0, prefixEnd + 1);
      return new SplitPath(root, path.Substring(suffixStart));
    }

    public bool IsAbsolutePath(string path) {
      return !string.IsNullOrEmpty(path) && path[0] == '/';
    }

    public bool IsFileName(string path) {
      if (path == null)
        throw new ArgumentNullException(nameof(path));
      return path.IndexOf('/') < 0;
    }

    public string GetParent(string path) {
      if (string.IsNullOrEmpty(path))
        return null;

      var end = path.Length - 1;
      while (end >= 0 && path[end] == '/')
        end--;
      if (end < 0)
        return null;

      var sep = path.LastIndexOf('/', end);
      if (sep < 0)
        return null;
      while (sep > 0 && path[sep - 1] == '/')
        sep--;
      return sep == 0 ? (path[0] == '/' ? "/" : null) : path.Substring(0, sep);
    }

    public string GetFileName(string path) {
      if (string.IsNullOrEmpty(path))
        return path;
      var index = path.LastIndexOf('/');
      return index >= 0 ? path.Substring(index + 1) : path;
    }

    public string GetExtension(string path) {
      if (string.IsNullOrEmpty(path))
        return "";
      for (var i = path.Length - 1; i >= 0; i--) {
        var ch = path[i];
        if (ch == '.')
          return i != path.Length - 1 ? path.Substring(i) : "";
        if (ch == '/')
          break;
      }
      return "";
    }

    public bool IsValidBclPath(string path) {
      return !string.IsNullOrEmpty(path) && path.IndexOf('\0') < 0;
    }
  }

  public sealed class LinuxPathHelper : PosixPathHelper {
    public LinuxPathHelper() : base(caseSensitive: true) { }
  }

  public sealed class MacPathHelper : PosixPathHelper {
    public MacPathHelper() : base(caseSensitive: false) { }
  }
}
