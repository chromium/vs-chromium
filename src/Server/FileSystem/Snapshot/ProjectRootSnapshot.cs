// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem.Snapshot {
  public class ProjectRootSnapshot {
    private readonly IProject _project;
    private readonly DirectorySnapshot _directory;

    public ProjectRootSnapshot(IProject project, DirectorySnapshot directory) {
      _project = project;
      _directory = directory;
    }

    public IProject Project { get { return _project; } }
    public DirectorySnapshot Directory { get { return _directory; } }
  }
}