// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem.Builder {
  /// <summary>
  /// The differences between two snapshots of the directory tree of a project (the tree of the
  /// index cache and a scan of the file system), as synthetic file system changes: applied by the
  /// incremental update of the index (<see cref="FullPathChanges"/>), they turn the index of the
  /// first tree into the index of the second one.
  /// <list type="bullet">
  /// <item>Files of both trees with a different last write time or size (from the directory
  /// enumerations), or without contents in the index (<c>filesToReload</c>): changed.</item>
  /// <item>Files only in the new tree: created; only in the old tree: deleted.</item>
  /// <item>Directories only in the new tree (or whose symbolic link flag changed): created (their
  /// files are loaded); only in the old tree: deleted.</item>
  /// </list>
  /// </summary>
  public static class SnapshotDiff {
    public static List<PathChangeEntry> Compute(FullPath projectRoot, DirectorySnapshot oldRoot, DirectorySnapshot newRoot,
                                                ISet<FileName> filesToReload, CancellationToken cancellationToken) {
      var result = new List<PathChangeEntry>();
      CompareDirectories(projectRoot, oldRoot, newRoot, filesToReload, result, cancellationToken);
      return result;
    }

    private static void CompareDirectories(FullPath projectRoot, DirectorySnapshot oldDirectory, DirectorySnapshot newDirectory,
                                           ISet<FileName> filesToReload, List<PathChangeEntry> result,
                                           CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();

      // Files
      var oldFiles = SortedIndices(oldDirectory.ChildFiles, x => x.Name);
      var newFiles = SortedIndices(newDirectory.ChildFiles, x => x.Name);
      int i = 0, j = 0;
      while (i < oldFiles.Length || j < newFiles.Length) {
        var compare = i >= oldFiles.Length ? 1 :
                      j >= newFiles.Length ? -1 :
                      SystemPathComparer.CompareNames(oldDirectory.ChildFiles[oldFiles[i]].Name, newDirectory.ChildFiles[newFiles[j]].Name);
        if (compare < 0) {
          AddFile(projectRoot, oldDirectory.ChildFiles[oldFiles[i]], PathChangeKind.Deleted, result);
          i++;
        } else if (compare > 0) {
          AddFile(projectRoot, newDirectory.ChildFiles[newFiles[j]], PathChangeKind.Created, result);
          j++;
        } else {
          var oldFile = oldDirectory.ChildFiles[oldFiles[i]];
          if (oldDirectory.ChildFileMetadata[oldFiles[i]] != newDirectory.ChildFileMetadata[newFiles[j]] ||
              (filesToReload != null && filesToReload.Contains(oldFile))) {
            AddFile(projectRoot, newDirectory.ChildFiles[newFiles[j]], PathChangeKind.Changed, result);
          }
          i++;
          j++;
        }
      }

      // Directories
      var oldDirectories = SortedIndices(oldDirectory.ChildDirectories, x => x.DirectoryName.Name);
      var newDirectories = SortedIndices(newDirectory.ChildDirectories, x => x.DirectoryName.Name);
      i = 0;
      j = 0;
      while (i < oldDirectories.Length || j < newDirectories.Length) {
        var compare = i >= oldDirectories.Length ? 1 :
                      j >= newDirectories.Length ? -1 :
                      SystemPathComparer.CompareNames(oldDirectory.ChildDirectories[oldDirectories[i]].DirectoryName.Name,
                        newDirectory.ChildDirectories[newDirectories[j]].DirectoryName.Name);
        if (compare < 0) {
          AddDirectory(projectRoot, oldDirectory.ChildDirectories[oldDirectories[i]], PathChangeKind.Deleted, result);
          i++;
        } else if (compare > 0) {
          AddDirectory(projectRoot, newDirectory.ChildDirectories[newDirectories[j]], PathChangeKind.Created, result);
          j++;
        } else {
          var oldChild = oldDirectory.ChildDirectories[oldDirectories[i]];
          var newChild = newDirectory.ChildDirectories[newDirectories[j]];
          if (oldChild.IsSymLink != newChild.IsSymLink) {
            // Note: Another directory (e.g. a symbolic link replaced with a directory).
            AddDirectory(projectRoot, oldChild, PathChangeKind.Deleted, result);
            AddDirectory(projectRoot, newChild, PathChangeKind.Created, result);
          } else {
            CompareDirectories(projectRoot, oldChild, newChild, filesToReload, result, cancellationToken);
          }
          i++;
          j++;
        }
      }
    }

    private static void AddFile(FullPath projectRoot, FileName fileName, PathChangeKind kind, List<PathChangeEntry> result) {
      result.Add(new PathChangeEntry(projectRoot, fileName.RelativePath, kind, PathKind.File));
    }

    private static void AddDirectory(FullPath projectRoot, DirectorySnapshot directory, PathChangeKind kind, List<PathChangeEntry> result) {
      result.Add(new PathChangeEntry(projectRoot, directory.DirectoryName.RelativePath, kind, PathKind.Directory));
    }

    /// <summary>
    /// The indices of <paramref name="items"/> sorted by name (usually already sorted).
    /// </summary>
    private static int[] SortedIndices<T>(IList<T> items, Func<T, string> getName) {
      var result = new int[items.Count];
      var sorted = true;
      for (var i = 0; i < result.Length; i++) {
        result[i] = i;
        if (i > 0 && SystemPathComparer.CompareNames(getName(items[i - 1]), getName(items[i])) > 0)
          sorted = false;
      }
      if (!sorted) {
        Array.Sort(result, (x, y) => SystemPathComparer.CompareNames(getName(items[x]), getName(items[y])));
      }
      return result;
    }
  }
}
