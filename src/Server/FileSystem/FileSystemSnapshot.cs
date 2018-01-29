// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    public FileSystemSnapshot(int version, ReadOnlyCollection<ProjectRootSnapshot> projectRoots) {
      _version = version;
      _projectRoots = projectRoots;
    }

    public int Version => _version;

    public IList<ProjectRootSnapshot> ProjectRoots => _projectRoots;
  }
}