// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Server.Cache {
  /// <summary>
  /// Cleanup of the cache root directory (one directory per project cache): deletes the caches of
  /// projects not used for <see cref="DefaultMaxAge"/>, then the least recently used ones while the
  /// total size is more than <see cref="DefaultMaxTotalBytes"/>. Never deletes the caches in use: open
  /// by this server, or locked by another server (their lock file can't be opened exclusively).
  /// <para>
  /// The last use of a project cache is the last write time of its index (written by the saves) or of
  /// its <c>root.txt</c> (written each time a server opens it to write it), whichever is the most
  /// recent: no additional bookkeeping.
  /// </para>
  /// </summary>
  public static class IndexCacheCleanup {
    /// <summary>The caches of projects not used for this delay are deleted.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(30);

    /// <summary>
    /// The maximum total size of the caches: the least recently used ones are deleted above it
    /// (fixed: about 5 times the cache of a 2.2M file tree).
    /// </summary>
    public const long DefaultMaxTotalBytes = 50L * 1024 * 1024 * 1024;

    /// <summary>The cleanup runs this delay after the first project cache is opened.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMinutes(2);

    private const string LockFileName = "lock";

    public sealed class CacheDirectory {
      public string Path;
      public DateTime LastUsedUtc;
      public long SizeBytes;
      public bool InUse;
    }

    /// <summary>
    /// Deletes the caches not used for <paramref name="maxAge"/>, then the least recently used ones
    /// while the total size is more than <paramref name="maxTotalBytes"/>. <paramref
    /// name="openDirectories"/>: the caches open by this server. Returns the deleted directories.
    /// </summary>
    public static List<string> Run(string cacheRoot, ICollection<string> openDirectories, TimeSpan maxAge, long maxTotalBytes,
                                   DateTime nowUtc) {
      var deleted = new List<string>();
      if (!Directory.Exists(cacheRoot))
        return deleted;
      var open = new HashSet<string>(openDirectories.Select(Normalize), StringComparer.OrdinalIgnoreCase);
      var caches = new List<CacheDirectory>();
      foreach (var path in Directory.GetDirectories(cacheRoot)) {
        try {
          caches.Add(Describe(path, open.Contains(Normalize(path))));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
          Logger.LogInfo("Cannot inspect the index cache directory \"{0}\": {1}", path, e.Message);
        }
      }

      var total = caches.Sum(x => x.SizeBytes);
      // Least recently used first.
      foreach (var cache in caches.OrderBy(x => x.LastUsedUtc)) {
        var tooOld = nowUtc - cache.LastUsedUtc > maxAge;
        if (!tooOld && total <= maxTotalBytes)
          break;
        if (cache.InUse)
          continue;
        if (TryDelete(cache.Path)) {
          Logger.LogInfo("Deleted the index cache \"{0}\" ({1:n0} bytes, last used {2:O}): {3}.", cache.Path, cache.SizeBytes,
            cache.LastUsedUtc, tooOld ? "not used for " + maxAge.TotalDays + " days" : "total size above the limit");
          total -= cache.SizeBytes;
          deleted.Add(cache.Path);
        }
      }
      return deleted;
    }

    private static string Normalize(string path) {
      return System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
    }

    private static CacheDirectory Describe(string path, bool open) {
      var result = new CacheDirectory { Path = path, InUse = open, LastUsedUtc = Directory.GetLastWriteTimeUtc(path) };
      foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories)) {
        result.SizeBytes += file.Length;
        if (file.Name == IndexCacheFile.IndexFileName || file.Name == "root.txt") {
          if (file.LastWriteTimeUtc > result.LastUsedUtc)
            result.LastUsedUtc = file.LastWriteTimeUtc;
        }
      }
      return result;
    }

    /// <summary>
    /// Deletes a cache directory, unless another server uses it (it holds its lock file).
    /// </summary>
    private static bool TryDelete(string path) {
      var lockPath = System.IO.Path.Combine(path, LockFileName);
      FileStream lockFile = null;
      try {
        // Note: While deleting, no server can open the cache to write it.
        lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1);
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) {
          if (!string.Equals(file, lockPath, StringComparison.OrdinalIgnoreCase)) {
            File.Delete(file);
          }
        }
        lockFile.Dispose();
        lockFile = null;
        Directory.Delete(path, recursive: true);
        return true;
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        Logger.LogInfo("Cannot delete the index cache \"{0}\" (in use?): {1}", path, e.Message);
        return false;
      }
      finally {
        lockFile?.Dispose();
      }
    }
  }
}
