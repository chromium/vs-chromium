// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Server.Projects {
  public interface IProject {
    FullPath RootPath { get; }

    IDirectoryFilter DirectoryFilter { get; }
    IFileFilter FileFilter { get; }
    ISearchableFilesFilter SearchableFilesFilter { get; }

    /// <summary>
    /// An opaque string unique to the project configuration.
    /// </summary>
    string VersionHash { get; }
  }
}
