// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Server.Cache {
  /// <summary>
  /// The options of the persistent index cache (see <see cref="IndexCache"/>).
  /// </summary>
  public sealed class IndexCacheOptions {
    /// <summary>
    /// Command line option: the root directory of the cache (one sub-directory per project).
    /// </summary>
    public const string CacheDirectoryArgument = "--cache-dir";

    /// <summary>
    /// Command line option: disables the cache.
    /// </summary>
    public const string NoCacheArgument = "--no-cache";

    /// <summary>
    /// Environment variable: the root directory of the cache (used if there is no command line
    /// option), or "none" to disable the cache.
    /// </summary>
    public const string CacheDirectoryVariable = "VSCHROMIUM_CACHE_DIR";

    /// <summary>
    /// Test only: the delays of the saves of the cache, in milliseconds: "settle,max" (see
    /// <see cref="SaveSettleDelay"/> and <see cref="SaveMaxDelay"/>).
    /// </summary>
    public const string SaveDelaysVariable = "VSCHROMIUM_TEST_CACHE_SAVE_DELAYS_MS";

    /// <summary>The cache is saved after this delay without index update...</summary>
    public static readonly TimeSpan DefaultSaveSettleDelay = TimeSpan.FromSeconds(30);

    /// <summary>... but at most this delay after the first unsaved index update.</summary>
    public static readonly TimeSpan DefaultSaveMaxDelay = TimeSpan.FromMinutes(5);

    public IndexCacheOptions(string cacheDirectory) {
      CacheDirectory = cacheDirectory;
      SaveSettleDelay = DefaultSaveSettleDelay;
      SaveMaxDelay = DefaultSaveMaxDelay;
      var delays = Environment.GetEnvironmentVariable(SaveDelaysVariable)?.Split(',');
      if (delays != null && delays.Length == 2 && int.TryParse(delays[0], out var settle) && int.TryParse(delays[1], out var max)) {
        SaveSettleDelay = TimeSpan.FromMilliseconds(settle);
        SaveMaxDelay = TimeSpan.FromMilliseconds(max);
      }
    }

    /// <summary>The cache is saved once the index is not updated during this delay (see <see cref="IndexCacheSaveScheduler"/>).</summary>
    public TimeSpan SaveSettleDelay { get; set; }

    /// <summary>The cache is saved at most this delay after the first unsaved index update.</summary>
    public TimeSpan SaveMaxDelay { get; set; }

    /// <summary>
    /// The root directory of the cache, or null if the cache is disabled.
    /// </summary>
    public string CacheDirectory { get; }

    public bool IsEnabled => CacheDirectory != null;

    public static IndexCacheOptions Disabled { get; } = new IndexCacheOptions(null);

    /// <summary>
    /// The options from the command line arguments (<see cref="CacheDirectoryArgument"/>,
    /// <see cref="NoCacheArgument"/>), then the environment (<see cref="CacheDirectoryVariable"/>),
    /// then the default directory of the platform (<see cref="GetDefaultCacheDirectory"/>).
    /// </summary>
    public static IndexCacheOptions FromCommandLine(string[] args) {
      for (var i = 0; i < args.Length; i++) {
        if (args[i] == NoCacheArgument) {
          return Disabled;
        }
        if (args[i] == CacheDirectoryArgument && i + 1 < args.Length) {
          return new IndexCacheOptions(Path.GetFullPath(args[i + 1]));
        }
      }

      var variable = Environment.GetEnvironmentVariable(CacheDirectoryVariable);
      if (!string.IsNullOrEmpty(variable)) {
        return string.Equals(variable, "none", StringComparison.OrdinalIgnoreCase)
          ? Disabled
          : new IndexCacheOptions(Path.GetFullPath(variable));
      }

      var directory = GetDefaultCacheDirectory();
      return directory == null ? Disabled : new IndexCacheOptions(directory);
    }

    /// <summary>
    /// The default root directory of the cache: <c>%LOCALAPPDATA%\VsChromium\Cache</c> on
    /// Windows, <c>~/Library/Caches/VsChromium</c> on macOS, <c>$XDG_CACHE_HOME/vschromium</c> or
    /// <c>~/.cache/vschromium</c> on Linux. Null if the directory of the user is unknown.
    /// </summary>
    public static string GetDefaultCacheDirectory() {
      if (OperatingSystem.IsWindows()) {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(localAppData) ? null : Path.Combine(localAppData, "VsChromium", "Cache");
      }

      var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      if (OperatingSystem.IsMacOS()) {
        return string.IsNullOrEmpty(home) ? null : Path.Combine(home, "Library", "Caches", "VsChromium");
      }

      var xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
      if (!string.IsNullOrEmpty(xdgCacheHome) && Path.IsPathRooted(xdgCacheHome)) {
        return Path.Combine(xdgCacheHome, "vschromium");
      }
      return string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".cache", "vschromium");
    }
  }
}
