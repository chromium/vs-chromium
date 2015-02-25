// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemSnapshot {
  public class FullPathChanges {
    private readonly Dictionary<FullPath, PathChangeKind> _map;
    private readonly Dictionary<FullPath, List<FullPath>> _createdChildren; 

    public FullPathChanges(IList<PathChangeEntry> entries) {
      _map = entries.
        ToDictionary(x => x.Path, x => x.Kind);
      _createdChildren = entries
        .Where(x => x.Kind == PathChangeKind.Created)
        .GroupBy(x => x.Path.Parent)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Path).ToList());
    }

    public PathChangeKind GetPathChangeKind(FullPath path) {
      PathChangeKind result;
      if (!_map.TryGetValue(path, out result)) {
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

    public IEnumerable<FullPath> GetCreatedEntries(FullPath parentPath) {
      List<FullPath> result;
      if (_createdChildren.TryGetValue(parentPath, out result)) {
        return result;
      }
      return Enumerable.Empty<FullPath>();
    }
  }
}