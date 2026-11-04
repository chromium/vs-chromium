// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Diagnostics {
  /// <summary>
  /// The memory of the file contents of a database, per project (see <see cref="ProjectArenaAllocators"/>).
  /// </summary>
  public static class FileContentsStatistics {
    /// <summary>
    /// The arenas of <paramref name="allocator"/> and the bytes used by <paramref name="contents"/>
    /// (contents allocated by this allocator, in dedicated blocks of large files, or in the memory
    /// mapped arena files of the index cache, see <see cref="ArenaStatistics.CachedBytes"/>).
    /// </summary>
    public static ArenaStatistics GetArenaStatistics(ArenaAllocator allocator, IEnumerable<FileContents> contents) {
      long cachedBytes = 0;
      var slots = new List<(SafeHandle Block, int SlotSize)>();
      foreach (var item in contents) {
        if (item is null or BinaryFileContents)
          continue;
        var slot = FileContentsFactory.GetArenaSlot(item);
        if (slot.Block is SafeMemoryMappedViewHandle) {
          cachedBytes += slot.SlotSize;
        } else {
          slots.Add(slot);
        }
      }
      var result = allocator.GetStatistics(slots);
      result.CachedBytes = cachedBytes;
      return result;
    }

    /// <summary>
    /// The ranges of the files of each project in the contents array of <paramref name="database"/>
    /// (from the ranges of its directories: one walk of the parents per directory, not per file).
    /// </summary>
    public static Dictionary<FullPath, List<FileRange>> GetProjectFileRanges(FileDatabaseSnapshot database) {
      var result = new Dictionary<FullPath, List<FileRange>>();
      foreach (var (directory, range) in database.Directories) {
        var root = directory;
        while (root != null && !root.IsAbsoluteName) {
          root = root.Parent;
        }
        if (root == null)
          continue;
        if (!result.TryGetValue(root.FullPath, out var ranges)) {
          ranges = new List<FileRange>();
          result.Add(root.FullPath, ranges);
        }
        ranges.Add(range);
      }
      return result;
    }

    /// <summary>The contents of the files in <paramref name="ranges"/> (null for files without contents).</summary>
    public static IEnumerable<FileContents> GetContents(FileDatabaseSnapshot database, IEnumerable<FileRange> ranges) {
      var contents = database.ContentsArray;
      foreach (var range in ranges) {
        for (var i = range.Start; i < range.Start + range.Count; i++) {
          yield return contents[i];
        }
      }
    }

    /// <summary>
    /// The memory of the file contents of each project of <paramref name="database"/> (with the
    /// allocator of the project), by project root path. Projects without allocator (no file read)
    /// count their mapped contents only.
    /// </summary>
    public static Dictionary<FullPath, ArenaStatistics> GetProjectStatistics(ProjectArenaAllocators allocators,
                                                                            IFileDatabaseSnapshot database) {
      var result = new Dictionary<FullPath, ArenaStatistics>();
      if (database is not FileDatabaseSnapshot snapshot)
        return result;
      foreach (var (root, ranges) in GetProjectFileRanges(snapshot)) {
        var allocator = allocators.TryGet(root, out var projectAllocator) ? projectAllocator : EmptyAllocator;
        result.Add(root, GetArenaStatistics(allocator, GetContents(snapshot, ranges)));
      }
      // Projects with arenas but no file in the database (e.g. a load in progress).
      foreach (var (root, allocator) in allocators.GetAll()) {
        if (!result.ContainsKey(root)) {
          result.Add(root, GetArenaStatistics(allocator, Enumerable.Empty<FileContents>()));
        }
      }
      return result;
    }

    /// <summary>The sum of the statistics of the projects.</summary>
    public static ArenaStatistics Sum(IEnumerable<ArenaStatistics> statistics) {
      var result = new ArenaStatistics();
      foreach (var item in statistics) {
        result.ArenaCount += item.ArenaCount;
        result.ArenaBytes += item.ArenaBytes;
        result.UsedBytes += item.UsedBytes;
        result.LiveBytes += item.LiveBytes;
        result.SparseArenaCount += item.SparseArenaCount;
        result.OtherBytes += item.OtherBytes;
        result.CachedBytes += item.CachedBytes;
      }
      return result;
    }

    /// <summary>An allocator without arena (projects whose files were not read).</summary>
    private static readonly ArenaAllocator EmptyAllocator = new ArenaAllocator();
  }
}
