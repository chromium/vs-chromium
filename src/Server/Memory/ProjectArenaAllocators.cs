// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Files;

namespace VsChromium.Core.Memory {
  /// <summary>
  /// The arena allocators of the file contents, one per project (by project root path): the native
  /// memory of each project (arenas, dead and free bytes) is known exactly, and the arenas of a
  /// project are released once the project is removed and its file contents are not referenced
  /// anymore (instead of becoming dead bytes in arenas shared with the other projects).
  /// Thread safe.
  /// </summary>
  public sealed class ProjectArenaAllocators {
    private readonly ConcurrentDictionary<FullPath, ArenaAllocator> _allocators = new ConcurrentDictionary<FullPath, ArenaAllocator>();
    private readonly Func<ArenaAllocator> _factory;

    public ProjectArenaAllocators() : this(() => new ArenaAllocator()) {
    }

    public ProjectArenaAllocators(Func<ArenaAllocator> factory) {
      _factory = factory;
    }

    /// <summary>The allocator of the project whose root is <paramref name="projectRoot"/> (created on first use).</summary>
    public ArenaAllocator Get(FullPath projectRoot) {
      if (_allocators.TryGetValue(projectRoot, out var allocator))
        return allocator;
      return _allocators.GetOrAdd(projectRoot, _ => _factory());
    }

    /// <summary>The allocator of a project, if it has one.</summary>
    public bool TryGet(FullPath projectRoot, out ArenaAllocator allocator) {
      return _allocators.TryGetValue(projectRoot, out allocator);
    }

    /// <summary>The projects and their allocator.</summary>
    public IReadOnlyList<KeyValuePair<FullPath, ArenaAllocator>> GetAll() {
      return _allocators.ToList();
    }

    /// <summary>
    /// Forgets the allocators of the projects not in <paramref name="projectRoots"/> (e.g. the
    /// projects removed from the index): their arenas are released when the file contents of the
    /// projects are not referenced anymore (old snapshots, running searches).
    /// </summary>
    public void Retain(IEnumerable<FullPath> projectRoots) {
      var roots = new HashSet<FullPath>(projectRoots);
      foreach (var root in _allocators.Keys) {
        if (!roots.Contains(root)) {
          _allocators.TryRemove(root, out _);
        }
      }
    }
  }
}
