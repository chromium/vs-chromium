// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.Projects {
  public interface IProjectDiscoveryProvider {
    /// <summary>
    /// Priority order (higher priority wins).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Returns the |IProject| corresponding to the project containing |filename|.
    /// Returns |null| if |filename| is not known to this project provider.
    /// </summary>
    IProject GetProject(string filename);

    /// <summary>
    /// Returns the |IProject| corresponding to the project root path |projectRootPath|.
    /// Returns |null| if |projectRootPath| is not known to this project provider.
    /// </summary>
    IProject GetProjectFromRootPath(string projectRootPath);

    /// <summary>
    /// Reset internal cache, usually called when something drastic happened on the file system.
    /// </summary>
    void ValidateCache();
  }
}
