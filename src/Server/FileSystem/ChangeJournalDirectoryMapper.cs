// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Files.Windows;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// Maps the records of the change journal (file id, parent directory id, reasons: the
  /// unprivileged records have no name) to the directories of a tree that must be listed again:
  /// <list type="bullet">
  /// <item>a record whose parent id is a known directory marks that directory (an entry was
  /// created, deleted, renamed, or a file was modified);</item>
  /// <item>a record whose id is a known directory (deleted, renamed, moved) marks the parent of that
  /// directory in the tree (its old parent: the record of a move out of the project has the new
  /// parent);</item>
  /// <item>unknown ids are directories outside the project (or ignored), or new directories: these
  /// are found by the listing of their parent (marked by their records) and scanned
  /// entirely.</item>
  /// </list>
  /// For records of directories, only the reasons that change the listing of their parent (create,
  /// delete, rename, reparse point) count: the time and size of a directory are not used.
  /// The directories of the subtrees of directory symbolic links and junctions are not mapped
  /// (their ids are ids of another volume, possibly): these subtrees are scanned entirely.
  /// </summary>
  public sealed class ChangeJournalDirectoryMapper {
    /// <summary>The reasons of directory records that change the listing of their parent.</summary>
    public const uint DirectoryListingReasons = UsnRecordParser.FileCreate | UsnRecordParser.FileDelete |
                                                UsnRecordParser.RenameOldName | UsnRecordParser.RenameNewName |
                                                UsnRecordParser.ReparsePointChange;

    private readonly Dictionary<UInt128, (DirectorySnapshot Directory, DirectorySnapshot Parent)> _directories;
    // Note: Reference equality (DirectorySnapshot doesn't override Equals).
    private readonly HashSet<DirectorySnapshot> _changed = new HashSet<DirectorySnapshot>();

    public ChangeJournalDirectoryMapper(DirectorySnapshot root) {
      _directories = new Dictionary<UInt128, (DirectorySnapshot, DirectorySnapshot)>();
      AddDirectories(root, null);
    }

    private void AddDirectories(DirectorySnapshot directory, DirectorySnapshot parent) {
      var stack = new Stack<(DirectorySnapshot Directory, DirectorySnapshot Parent)>();
      stack.Push((directory, parent));
      while (stack.Count > 0) {
        var (d, p) = stack.Pop();
        if (d.FileId != UInt128.Zero) {
          // Note: Ids are unique on a volume (a duplicate is a hard link to a directory: not possible).
          _directories[d.FileId] = (d, p);
        } else {
          // No id (e.g. created by an incremental update whose id couldn't be read): always listed again.
          ZeroIdDirectoryCount++;
        }
        if (d.IsSymLink)
          continue;
        foreach (var child in d.ChildDirectories.ToForeachEnum()) {
          stack.Push((child, d));
        }
      }
    }

    /// <summary>The directories with an id (mapped).</summary>
    public int DirectoryCount => _directories.Count;

    /// <summary>The directories without id (outside of symbolic links): they are always listed again.</summary>
    public int ZeroIdDirectoryCount { get; private set; }

    /// <summary>The records processed.</summary>
    public long RecordCount { get; private set; }

    /// <summary>The directories to list again (reference equality).</summary>
    public IReadOnlySet<DirectorySnapshot> ChangedDirectories => _changed;

    public void Add(in ChangeJournalRecord record) {
      RecordCount++;
      // Directory records only change the listing of their parent if they are created, deleted,
      // renamed or replaced (reparse point).
      if (record.IsDirectory && (record.Reason & DirectoryListingReasons) == 0)
        return;
      if (_directories.TryGetValue(record.ParentFileId, out var parent)) {
        _changed.Add(parent.Directory);
      }
      if (_directories.TryGetValue(record.FileId, out var self) && self.Parent != null) {
        _changed.Add(self.Parent);
      }
    }
  }
}
