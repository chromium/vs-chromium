// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Cache {
  /// <summary>
  /// The tree of a project from its index cache (phase 2): the directory snapshot of the project
  /// as it was when the cache was written, and the files without cached contents (read from disk
  /// by the verification of the tree).
  /// </summary>
  public sealed class CachedProjectTree {
    public CachedProjectTree(DirectorySnapshot root, IReadOnlyCollection<FileName> filesWithoutContents,
                             bool filesWithoutContentsAreLarge, ChangeJournalCheckpoint checkpoint = null) {
      Root = root;
      FilesWithoutContents = filesWithoutContents;
      FilesWithoutContentsAreLarge = filesWithoutContentsAreLarge;
      Checkpoint = checkpoint;
    }

    /// <summary>
    /// The change journal checkpoint of the tree (every change before it is in the tree), or null:
    /// the verification of the tree uses the change journal from there if possible.
    /// </summary>
    public ChangeJournalCheckpoint Checkpoint { get; }

    /// <summary>
    /// True if the files without contents are all larger than the cached files (not cached
    /// because of their size, not because they were modified when the cache was written).
    /// </summary>
    public bool FilesWithoutContentsAreLarge { get; }

    public DirectorySnapshot Root { get; }

    /// <summary>
    /// The searchable files whose contents are not in the cache (larger than 512 KB, modified
    /// during the load or after the scan when the cache was written).
    /// </summary>
    public IReadOnlyCollection<FileName> FilesWithoutContents { get; }
  }

  /// <summary>
  /// Creates the directory snapshot of a project from the tree of its index cache.
  /// </summary>
  public static class CachedTreeBuilder {
    public static CachedProjectTree Build(IndexCacheIndex index, IFileSystemNameFactory nameFactory, FullPath rootPath,
                                          CancellationToken cancellationToken) {
      var directories = index.Directories;
      var names = new DirectoryName[directories.Length];
      var children = new List<int>[directories.Length];
      names[0] = nameFactory.CreateAbsoluteDirectoryName(rootPath);
      for (var i = 1; i < directories.Length; i++) {
        var directory = directories[i];
        var isSymLink = (directory.Flags & IndexCacheDirectoryFlags.SymLink) != 0;
        names[i] = nameFactory.CreateDirectoryName(names[directory.Parent], index.GetDirectoryName(i).ToString(), isSymLink);
        (children[directory.Parent] ??= new List<int>()).Add(i);
      }

      // Bottom up: the children of a directory are after it (depth first order).
      var withoutContents = new List<FileName>();
      var withoutContentsAreLarge = true;
      var snapshots = new DirectorySnapshot[directories.Length];
      for (var i = directories.Length - 1; i >= 0; i--) {
        if ((i & 1023) == 0)
          cancellationToken.ThrowIfCancellationRequested();
        var directory = directories[i];
        var fileNames = new FileName[directory.FileCount];
        var metadata = directory.FileCount == 0 ? Array.Empty<FileMetadata>() : new FileMetadata[directory.FileCount];
        List<int> symLinkFiles = null;
        for (var j = 0; j < directory.FileCount; j++) {
          var fileIndex = directory.FileStart + j;
          var file = index.Files[fileIndex];
          fileNames[j] = nameFactory.CreateFileName(names[i], index.GetName(fileIndex).ToString());
          metadata[j] = new FileMetadata(new DateTime(file.LastWriteTimeTicks, DateTimeKind.Utc), file.FileLength);
          if ((file.Flags & IndexCacheEntryFlags.SymLink) != 0) {
            (symLinkFiles ??= new List<int>()).Add(j);
          }
          if ((file.Flags & IndexCacheEntryFlags.NoContents) != 0) {
            withoutContents.Add(fileNames[j]);
            withoutContentsAreLarge &= file.FileLength > ProjectIndexCache.MaxCachedFileLength;
          }
        }
        IList<DirectorySnapshot> childSnapshots;
        if (children[i] == null) {
          childSnapshots = Array.Empty<DirectorySnapshot>();
        } else {
          var list = new DirectorySnapshot[children[i].Count];
          for (var j = 0; j < list.Length; j++) {
            list[j] = snapshots[children[i][j]];
            snapshots[children[i][j]] = null;
          }
          childSnapshots = list;
        }
        var isSymLink = (directory.Flags & IndexCacheDirectoryFlags.SymLink) != 0;
        snapshots[i] = new DirectorySnapshot(new DirectoryData(names[i], isSymLink, directory.FileId), childSnapshots.ToReadOnlyList(),
          fileNames.ToReadOnlyList(), metadata, symLinkFiles?.ToArray());
      }
      return new CachedProjectTree(snapshots[0], withoutContents, withoutContentsAreLarge, index.Checkpoint);
    }
  }
}
