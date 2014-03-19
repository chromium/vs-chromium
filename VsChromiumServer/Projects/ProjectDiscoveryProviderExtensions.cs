// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.Projects {
  public static class ProjectDiscoveryProviderExtensions {
    /// <summary>
    /// Returns the absolute path of the project containing |filename|.
    /// Returns |null| if |filename| is not located within a local project directory.
    /// </summary>
    public static string GetProjectPath(this IProjectDiscoveryProvider provider, string filename) {
      var project = provider.GetProject(filename);
      if (project == null)
        return null;
      return project.RootPath;
    }
  }
}
