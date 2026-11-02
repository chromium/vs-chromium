// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase.Builder {
  public static class ProjectDiscoveryExtensions {
    public static bool IsFileSearchable(this IProject project, FileName filename) {
      // Note: The relative path is built in a temporary buffer (no allocation per file).
      return filename.TestRelativePath(static (path, filter) => filter.Include(path), project.SearchableFilesFilter);
    }
  }
}
