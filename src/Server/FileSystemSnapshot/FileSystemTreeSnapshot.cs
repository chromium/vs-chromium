// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.ObjectModel;
using System.Linq;
using VsChromium.Core.Linq;

namespace VsChromium.Server.FileSystemSnapshot {
  /// <summary>
  /// A view of the file system (directories and files) of all the projects
  /// known to the server at the time the snapshot was built.
  /// </summary>
  public class FileSystemTreeSnapshot {
    private readonly int _version;
    private readonly ReadOnlyCollection<ProjectRootSnapshot> _projectRoots;

    public FileSystemTreeSnapshot(int version, ReadOnlyCollection<ProjectRootSnapshot> projectRoots) {
      _version = version;
      _projectRoots = projectRoots;
    }

    public int Version { get { return _version; } }
    public ReadOnlyCollection<ProjectRootSnapshot> ProjectRoots { get { return _projectRoots; } }

    public static FileSystemTreeSnapshot Empty {
      get {
        return new FileSystemTreeSnapshot(0, Enumerable.Empty<ProjectRootSnapshot>().ToReadOnlyCollection());
      }
    }
  }
}