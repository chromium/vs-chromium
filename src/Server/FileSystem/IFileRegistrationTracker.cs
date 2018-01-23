// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Files;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public interface IFileRegistrationTracker {
    /// <summary>
    /// Register a new file to serve as the base for figuring out project roots
    /// </summary>
    void RegisterFileAsync(FullPath path);

    /// <summary>
    /// Un-register a new file to serve as the base for figuring out project roots
    /// </summary>
    void UnregisterFileAsync(FullPath path);

    /// <summary>
    /// Force a file system rescan
    /// </summary>
    void RefreshAsync(Action<IList<IProject>> callback);

    event EventHandler<ProjectsEventArgs> ProjectListChanged;
  }

  public class ProjectsEventArgs : EventArgs {
    private readonly IList<IProject> _projects;

    public ProjectsEventArgs(IList<IProject> projects) {
      _projects = projects;
    }

    public IList<IProject> Projects {
      get { return _projects; }
    }
  }
}