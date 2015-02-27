// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemSnapshot {
  public class FileSystemSnapshotVisitor {

    public static List<KeyValuePair<IProject, DirectorySnapshot>> GetDirectories(FileSystemTreeSnapshot snapshot) {
      var result = new List<KeyValuePair<IProject, DirectorySnapshot>>();
      VisitDirectories(snapshot, (project, directory) => {
        result.Add(new KeyValuePair<IProject, DirectorySnapshot>(project, directory));
      });
      return result;
    }

    public static void VisitDirectories(
      FileSystemTreeSnapshot snapshot,
      Action<IProject, DirectorySnapshot> callback) {
      foreach (var project in snapshot.ProjectRoots.ToForeachEnum()) {
        VisitDirectory(project.Project, project.Directory, callback);
      }
    }

    private static void VisitDirectory(
      IProject project,
      DirectorySnapshot directory,
      Action<IProject, DirectorySnapshot> callback) {
      callback(project, directory);
      foreach (var child in directory.ChildDirectories) {
        VisitDirectory(project, child, callback);
      }
    }
  }
}
