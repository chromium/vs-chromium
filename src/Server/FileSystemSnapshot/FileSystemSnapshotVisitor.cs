// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Linq;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemSnapshot {
  public class FileSystemSnapshotVisitor {
    public static IEnumerable<KeyValuePair<IProject, DirectorySnapshot>> GetDirectories(FileSystemTreeSnapshot snapshot) {

      var stack = new Stack<KeyValuePair<IProject, DirectorySnapshot>>();

      foreach (var project in snapshot.ProjectRoots) {
        stack.Push(new KeyValuePair<IProject, DirectorySnapshot>(project.Project, project.Directory));
      }

      while (!stack.IsEmpty()) {
        var head = stack.Pop();
        foreach (var directory in head.Value.DirectoryEntries) {
          stack.Push(new KeyValuePair<IProject, DirectorySnapshot>(head.Key, directory));
        }
        yield return head;
      }
    }
  }
}
