// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// Map from FullPath to PathChangeKind.
  /// Note: This class is thread safe
  /// </summary>
  public class FullPathChanges {
    private readonly IList<PathChangeEntry> _entries;
    private readonly Lazy<Dictionary<FullPath, PathChangeKind>> _map;
    private readonly Lazy<HashSet<FullPath>> _createdDirectories;
    private readonly Lazy<HashSet<FullPath>> _basePaths;

    public FullPathChanges(IList<PathChangeEntry> entries) {
      _entries = entries;

      _map = new Lazy<Dictionary<FullPath, PathChangeKind>>(() =>
        _entries.ToDictionary(x => x.Path, x => x.ChangeKind));

      _createdDirectories = new Lazy<HashSet<FullPath>>(() =>
        new HashSet<FullPath>(_entries.Where(MaybeDirectoryCreation).Select(entry => entry.Path)));

      _basePaths = new Lazy<HashSet<FullPath>>(() =>
        new HashSet<FullPath>(_entries.Select(x => x.BasePath).Distinct()));
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

    public bool ShouldSkipLoadFileContents(FullPath path) {
      // Don't skip if file has any change associated to it
      if (GetPathChangeKind(path) != PathChangeKind.None) {
        return false;
      }

      // Don't skip if any parent directory was newly created
      if (_createdDirectories.Value.Count > 0) {
        foreach (var parent in path.EnumerateParents()) {
          if (_createdDirectories.Value.Contains(parent)) {
            return false;
          }
          // Optimization: Don't move up past base (i.e. project) paths
          if (_basePaths.Value.Contains(parent)) {
            break;
          }
        }
      }

      return true;
    }

    private PathChangeKind GetPathChangeKind(FullPath path) {
      PathChangeKind result;
      if (!_map.Value.TryGetValue(path, out result)) {
        result = PathChangeKind.None;
      }
      return result;
    }
  }
}