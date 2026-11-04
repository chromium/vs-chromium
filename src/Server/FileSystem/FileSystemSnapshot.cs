// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// A view of the file system (directories and files) of all the projects
  /// known to the server at the time the snapshot was built.
  /// </summary>
  public class FileSystemSnapshot {
    public static readonly FileSystemSnapshot Empty =
      new FileSystemSnapshot(0, Enumerable.Empty<ProjectRootSnapshot>().ToReadOnlyCollection());

    private readonly int _version;
    private readonly ReadOnlyCollection<ProjectRootSnapshot> _projectRoots;
    private readonly IReadOnlyCollection<FullPath> _cachedProjectRoots;

    public FileSystemSnapshot(int version, ReadOnlyCollection<ProjectRootSnapshot> projectRoots)
      : this(version, projectRoots, Array.Empty<FullPath>()) {
    }

    public FileSystemSnapshot(int version, ReadOnlyCollection<ProjectRootSnapshot> projectRoots,
                              IReadOnlyCollection<FullPath> cachedProjectRoots) {
      _version = version;
      _projectRoots = projectRoots;
      _cachedProjectRoots = cachedProjectRoots;
    }

    public int Version => _version;

    public IList<ProjectRootSnapshot> ProjectRoots => _projectRoots;

    /// <summary>
    /// The root paths of the projects whose tree comes from the index cache and is not verified
    /// yet (phase 2 of the index cache: the tree of the last session, published immediately and
    /// verified by a scan in the background).
    /// </summary>
    public IReadOnlyCollection<FullPath> CachedProjectRoots => _cachedProjectRoots;

    /// <summary>
    /// True if the tree of at least one project comes from the index cache and is not verified yet.
    /// </summary>
    public bool IsFromCache => _cachedProjectRoots.Count > 0;

    public bool IsCachedProject(FullPath rootPath) {
      foreach (var root in _cachedProjectRoots) {
        if (root.Equals(rootPath))
          return true;
      }
      return false;
    }

    /// <summary>The same snapshot with other unverified cached projects.</summary>
    public FileSystemSnapshot WithCachedProjectRoots(IReadOnlyCollection<FullPath> cachedProjectRoots) {
      return new FileSystemSnapshot(_version, _projectRoots, cachedProjectRoots);
    }
  }
}
