// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Static facade for OS-aware path manipulation and comparisons.
  /// Delegates all calls to a lazily initialized <see cref="IPlatformPathHelper"/>
  /// (<see cref="WindowsPathHelper"/>, <see cref="LinuxPathHelper"/>, or <see cref="MacPathHelper"/>).
  /// </summary>
  public static class PathHelpers {
    private static IPlatformPathHelper _platform;

    /// <summary>
    /// Returns the lazily initialized <see cref="IPlatformPathHelper"/> for the current operating system.
    /// </summary>
    public static IPlatformPathHelper Platform => _platform ?? InitializePlatform();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IPlatformPathHelper InitializePlatform() {
      IPlatformPathHelper helper = OperatingSystem.IsWindows()
        ? new WindowsPathHelper()
        : OperatingSystem.IsLinux()
          ? new LinuxPathHelper()
          : OperatingSystem.IsMacOS()
            ? new MacPathHelper()
            : throw new PlatformNotSupportedException("Unsupported operating system platform.");
      Interlocked.CompareExchange(ref _platform, helper, null);
      return _platform;
    }

    public static char DirectorySeparatorChar => Platform.DirectorySeparatorChar;
    public static StringComparer FileNameComparer => Platform.FileNameComparer;
    public static StringComparison FileNameComparison => Platform.FileNameComparison;
    public static IPathComparer PathComparer => Platform.PathComparer;

    public static string CombinePaths(string path1, string path2) => Platform.CombinePaths(path1, path2);
    public static bool IsPrefix(string prefix, string path) => Platform.IsPrefix(prefix, path);
    public static IEnumerable<string> SplitPath(string path) => Platform.SplitPath(path);
    public static SplitPath SplitPrefix(string path, string prefix) => Platform.SplitPrefix(path, prefix);
    public static bool IsAbsolutePath(string path) => Platform.IsAbsolutePath(path);
    public static bool IsFileName(string path) => Platform.IsFileName(path);
    public static string GetParent(string path) => Platform.GetParent(path);
    public static string GetFileName(string path) => Platform.GetFileName(path);
    public static string GetExtension(string path) => Platform.GetExtension(path);
    public static bool IsValidBclPath(string path) => Platform.IsValidBclPath(path);
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
    /// separator (except when root itself is "/").
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
