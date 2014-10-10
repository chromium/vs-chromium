// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Utility;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemSnapshot {
  public class FileSystemSnapshotVisitor {
    public static IEnumerable<KeyValuePair<IProject, DirectorySnapshot>> GetDirectories(FileSystemTreeSnapshot snapshot) {
      var result = new List<KeyValuePair<IProject, DirectorySnapshot>>();
      foreach (var project in snapshot.ProjectRoots) {
        ProcessRoot(project, result);
      }
      return result;
    }

    private static void ProcessRoot(ProjectRootSnapshot project, List<KeyValuePair<IProject, DirectorySnapshot>> result) {
      ProcessDirectory(project.Project, project.Directory, result);
    }

    private static void ProcessDirectory(IProject project, DirectorySnapshot directory, List<KeyValuePair<IProject, DirectorySnapshot>> result) {
      result.Add(KeyValuePair.Create(project, directory));
      foreach (var child in directory.ChildDirectories) {
        ProcessDirectory(project, child, result);
      }
    }
  }
}
