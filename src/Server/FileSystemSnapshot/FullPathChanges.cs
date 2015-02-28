// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;

namespace VsChromium.Server.FileSystemSnapshot {
  /// <summary>
  /// Map from FullPath to PathChangeKind.
  /// Note: This class is thread safe
  /// </summary>
  public class FullPathChanges {
    private readonly IList<PathChangeEntry> _entries;
    private readonly Lazy<Dictionary<FullPath, PathChangeKind>> _map;

    public FullPathChanges(IList<PathChangeEntry> entries) {
      _entries = entries;
      _map = new Lazy<Dictionary<FullPath, PathChangeKind>>(() => _entries.ToDictionary(x => x.Path, x => x.Kind));
    }

    public IList<PathChangeEntry> Entries {
      get { return _entries; }
    }

    public PathChangeKind GetPathChangeKind(FullPath path) {
      PathChangeKind result;
      if (!_map.Value.TryGetValue(path, out result)) {
        result = PathChangeKind.None;
      }
      return result;
    }

    public bool IsDeleted(FullPath path) {
      return GetPathChangeKind(path) == PathChangeKind.Deleted;
    }

    public bool IsCreated(FullPath path) {
      return GetPathChangeKind(path) == PathChangeKind.Created;
    }

    public bool IsChanged(FullPath path) {
      return GetPathChangeKind(path) == PathChangeKind.Changed;
    }
  }

  /// <summary>
  /// Map from RelativePath to PathChangeKind for a given project root path.
  /// Note: This class is thread safe
  /// </summary>
  public class ProjectPathChanges {
    private readonly FullPath _projectPath;
    private readonly Dictionary<RelativePath, PathChangeKind> _map;
    private readonly Lazy<Dictionary<RelativePath, List<RelativePath>>> _createdChildren;
    private readonly Lazy<Dictionary<RelativePath, List<RelativePath>> >_deletedChildren;

    public ProjectPathChanges(FullPath projectPath, IList<PathChangeEntry> entries) {
      _projectPath = projectPath;

      _map = entries
        .Where(x => PathHelpers.IsPrefix(x.Path.Value, _projectPath.Value))
        .Select(x => {
          var relPath = PathHelpers.SplitPrefix(x.Path.Value, _projectPath.Value).Suffix;
          return KeyValuePair.Create(new RelativePath(relPath), x.Kind);
        })
        .ToDictionary(x => x.Key, x => x.Value);

      _createdChildren = new Lazy<Dictionary<RelativePath, List<RelativePath>>>(() => _map
        .Where(x => x.Value == PathChangeKind.Created)
        .GroupBy(x => x.Key.Parent)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList()));

      _deletedChildren = new Lazy<Dictionary<RelativePath, List<RelativePath>>>(() => _map
        .Where(x => x.Value == PathChangeKind.Deleted)
        .GroupBy(x => x.Key.Parent)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList()));
    }

    public PathChangeKind GetPathChangeKind(RelativePath path) {
      PathChangeKind result;
      if (!_map.TryGetValue(path, out result)) {
        result = PathChangeKind.None;
      }
      return result;
    }

    public bool IsDeleted(RelativePath path) {
      return GetPathChangeKind(path) == PathChangeKind.Deleted;
    }

    public bool IsCreated(RelativePath path) {
      return GetPathChangeKind(path) == PathChangeKind.Created;
    }

    public bool IsChanged(RelativePath path) {
      return GetPathChangeKind(path) == PathChangeKind.Changed;
    }

    public IList<RelativePath> GetCreatedEntries(RelativePath parentPath) {
      return _createdChildren.Value.GetValue(parentPath) ?? ArrayUtilities.EmptyList<RelativePath>.Instance;
    }

    public IList<RelativePath> GetDeletedEntries(RelativePath parentPath) {
      return _deletedChildren.Value.GetValue(parentPath) ?? ArrayUtilities.EmptyList<RelativePath>.Instance;
    }
  }
}