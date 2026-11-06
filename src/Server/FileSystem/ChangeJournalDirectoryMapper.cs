// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Files;
using VsChromium.Core.Files.Windows;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystem.Builder;

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
  /// The subtrees of directory symbolic links and junctions whose target is on the volume of the
  /// project are mapped too (their id is the id of their target, see <see
  /// cref="DirectoryData.FileId"/>; a target inside the project is in the tree twice: several
  /// directories per id); the others (no id: target on another volume) are not mapped and are
  /// scanned entirely. A record of the target of a symbolic link (deleted, renamed) marks the link
  /// to be scanned entirely (<see cref="SymLinksToScan"/>), as well as the links whose target
  /// changed (<see cref="CheckSymLinks"/>).
  /// </summary>
  public sealed class ChangeJournalDirectoryMapper {
    /// <summary>The reasons of directory records that change the listing of their parent.</summary>
    public const uint DirectoryListingReasons = UsnRecordParser.FileCreate | UsnRecordParser.FileDelete |
                                                UsnRecordParser.RenameOldName | UsnRecordParser.RenameNewName |
                                                UsnRecordParser.ReparsePointChange;

    /// <summary>The reasons of a record of the target of a symbolic link that make the link scanned entirely.</summary>
    private const uint SymLinkTargetReasons = UsnRecordParser.FileDelete | UsnRecordParser.RenameOldName |
                                              UsnRecordParser.RenameNewName | UsnRecordParser.ReparsePointChange;

    /// <summary>The directories of each id (usually one: several for a target of a symbolic link inside the project).</summary>
    private readonly Dictionary<UInt128, (DirectorySnapshot Directory, DirectorySnapshot Parent)> _directories =
      new Dictionary<UInt128, (DirectorySnapshot, DirectorySnapshot)>();
    private readonly Dictionary<UInt128, List<(DirectorySnapshot Directory, DirectorySnapshot Parent)>> _duplicates =
      new Dictionary<UInt128, List<(DirectorySnapshot, DirectorySnapshot)>>();
    private readonly List<DirectorySnapshot> _symLinks = new List<DirectorySnapshot>();
    // Note: Reference equality (DirectorySnapshot doesn't override Equals).
    private readonly HashSet<DirectorySnapshot> _changed = new HashSet<DirectorySnapshot>();
    private readonly HashSet<DirectorySnapshot> _symLinksToScan = new HashSet<DirectorySnapshot>();

    public ChangeJournalDirectoryMapper(DirectorySnapshot root) {
      AddDirectories(root, null);
    }

    private void AddDirectories(DirectorySnapshot directory, DirectorySnapshot parent) {
      var stack = new Stack<(DirectorySnapshot Directory, DirectorySnapshot Parent)>();
      stack.Push((directory, parent));
      while (stack.Count > 0) {
        var (d, p) = stack.Pop();
        if (d.FileId != UInt128.Zero) {
          if (!_directories.TryAdd(d.FileId, (d, p))) {
            if (!_duplicates.TryGetValue(d.FileId, out var list)) {
              _duplicates.Add(d.FileId, list = new List<(DirectorySnapshot, DirectorySnapshot)>());
            }
            list.Add((d, p));
          }
        } else if (!d.IsSymLink) {
          // No id (e.g. created by an incremental update whose id couldn't be read): always listed again.
          ZeroIdDirectoryCount++;
        }
        if (d.IsSymLink) {
          // Target on another volume: scanned entirely (not mapped).
          if (d.FileId == UInt128.Zero)
            continue;
          _symLinks.Add(d);
        }
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

    /// <summary>
    /// The directory symbolic links and junctions (mapped: target on the volume of the project) to
    /// scan entirely: their target was deleted or renamed, or changed (<see cref="CheckSymLinks"/>).
    /// </summary>
    public IReadOnlySet<DirectorySnapshot> SymLinksToScan => _symLinksToScan;

    /// <summary>The mapped directory symbolic links and junctions (target on the volume of the project).</summary>
    public IReadOnlyList<DirectorySnapshot> SymLinks => _symLinks;

    public void Add(in ChangeJournalRecord record) {
      RecordCount++;
      // Directory records only change the listing of their parent if they are created, deleted,
      // renamed or replaced (reparse point).
      if (record.IsDirectory && (record.Reason & DirectoryListingReasons) == 0)
        return;
      if (_directories.TryGetValue(record.ParentFileId, out var parent)) {
        _changed.Add(parent.Directory);
        if (_duplicates.TryGetValue(record.ParentFileId, out var others)) {
          foreach (var other in others) {
            _changed.Add(other.Directory);
          }
        }
      }
      if (_directories.TryGetValue(record.FileId, out var self)) {
        AddSelf(self, record.Reason);
        if (_duplicates.TryGetValue(record.FileId, out var others)) {
          foreach (var other in others) {
            AddSelf(other, record.Reason);
          }
        }
      }
    }

    private void AddSelf((DirectorySnapshot Directory, DirectorySnapshot Parent) entry, uint reason) {
      if (entry.Parent != null) {
        _changed.Add(entry.Parent);
      }
      // The target of a symbolic link deleted, renamed or moved (possibly outside of the project:
      // its parent is unknown): the link resolves elsewhere now.
      if (entry.Directory.IsSymLink && (reason & SymLinkTargetReasons) != 0) {
        _symLinksToScan.Add(entry.Directory);
      }
    }

    /// <summary>
    /// Resolves the target of each mapped directory symbolic link and junction again: if it is not
    /// the directory of its id anymore (retargeted link, target deleted and recreated, an ancestor of
    /// the target renamed, target on another volume now, dangling link), the link is scanned
    /// entirely (<see cref="SymLinksToScan"/>). One open per link. Returns the number of links checked.
    /// </summary>
    public int CheckSymLinks(IFileSystem fileSystem, FullPath rootPath, ulong projectVolume) {
      foreach (var symLink in _symLinks) {
        var id = ProjectRootSnapshotBuilder.GetSymLinkTargetId(fileSystem, rootPath.Combine(symLink.DirectoryName.RelativePath),
          projectVolume);
        if (id != symLink.FileId) {
          _symLinksToScan.Add(symLink);
        }
      }
      return _symLinks.Count;
    }
  }
}
