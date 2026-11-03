// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// Map from FullPath to PathChangeKind.
  /// Note: This class is thread safe
  /// </summary>
  public class FullPathChanges {
    private readonly IList<PathChangeEntry> _entries;
    private readonly Lazy<HashSet<FullPath>> _createdDirectories;
    private readonly Lazy<PathSets> _pathSets;

    public FullPathChanges(IList<PathChangeEntry> entries) {
      _entries = entries;

      _createdDirectories = new Lazy<HashSet<FullPath>>(() =>
        new HashSet<FullPath>(_entries.Where(MaybeDirectoryCreation).Select(entry => entry.Path)));

      _pathSets = new Lazy<PathSets>(() => new PathSets(_entries, _createdDirectories.Value));
    }

    private static bool MaybeDirectoryCreation(PathChangeEntry entry) {
      if (entry.ChangeKind == PathChangeKind.Created) {
        return entry.PathKind == PathKind.Directory
               || entry.PathKind == PathKind.FileOrDirectory
               || entry.PathKind == PathKind.FileAndDirectory;
      }
      return false;
    }

    public IList<PathChangeEntry> Entries {
      get { return _entries; }
    }

    /// <summary>
    /// Returns <code>true</code> if the contents of <paramref name="fileName"/> don't need to be
    /// reloaded, i.e. if neither the file nor any newly created parent directory has changed.
    /// Does not build the full path of <paramref name="fileName"/> (no allocation): called for
    /// every file of the index on incremental updates.
    /// </summary>
    public bool ShouldSkipLoadFileContents(FileName fileName) {
      return fileName.TestFullPath(static (path, sets) => sets.ShouldSkip(path), _pathSets.Value);
    }

    /// <summary>
    /// The changed paths and created directories as strings, with lookups by span.
    /// </summary>
    private sealed class PathSets {
      private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _changedPaths;
      private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _createdDirectories;
      private readonly bool _hasCreatedDirectories;

      public PathSets(IList<PathChangeEntry> entries, IEnumerable<FullPath> createdDirectories) {
        // Note: Same equality as FullPath (SystemPathComparer), with a comparer supporting span lookups.
        var comparer = PathComparerRegistry.Default.Comparison == StringComparison.Ordinal
          ? StringComparer.Ordinal
          : StringComparer.OrdinalIgnoreCase;
        var changed = new HashSet<string>(entries.Select(x => x.Path.Value), comparer);
        var created = new HashSet<string>(createdDirectories.Select(x => x.Value), comparer);
        _changedPaths = changed.GetAlternateLookup<ReadOnlySpan<char>>();
        _createdDirectories = created.GetAlternateLookup<ReadOnlySpan<char>>();
        _hasCreatedDirectories = created.Count > 0;
      }

      public bool ShouldSkip(ReadOnlySpan<char> path) {
        // Don't skip if path has any change associated to it
        if (_changedPaths.Contains(path))
          return false;

        // Don't skip if any parent directory was newly created
        if (_hasCreatedDirectories) {
          for (var i = path.Length - 1; i > 0; i--) {
            if (path[i] == PathHelpers.DirectorySeparatorChar && _createdDirectories.Contains(path.Slice(0, i)))
              return false;
          }
        }
        return true;
      }
    }
  }
}
