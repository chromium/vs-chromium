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
}