// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Server.Projects {
  public interface IProjectDiscoveryProvider {
    /// <summary>
    /// Priority order (higher priority wins).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Returns the <see cref="IProject"/> corresponding to the project
    /// containing <paramref name="path"/>. Returns <code>null</code> if
    /// <paramref name="path"/> is not known to this project provider.
    /// </summary>
    IProject GetProjectFromAnyPath(FullPath path);

    /// <summary>
    /// Returns the <see cref="IProject"/> corresponding to the project root
    /// path <paramref name="projectRootPath"/>. Returns <code>null</code> if
    /// <paramref name="projectRootPath"/> is not known to this project
    /// provider.
    /// </summary>
    IProject GetProjectFromRootPath(FullPath projectRootPath);

    /// <summary>
    /// Reset internal cache, usually called when something drastic happened on
    /// the file system.
    /// </summary>
    void ValidateCache();
  }
}
