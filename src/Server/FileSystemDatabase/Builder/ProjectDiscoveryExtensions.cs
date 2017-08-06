// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase.Builder {
  public static class ProjectDiscoveryExtensions {
    public static bool IsFileSearchable(this IProject project, FileName filename) {
      return project.SearchableFilesFilter.Include(filename.RelativePath);
    }
  }
}
