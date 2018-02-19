// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetDirectoryEntriesRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;

    [ImportingConstructor]
    public GetDirectoryEntriesRequestHandler(IFileSystemSnapshotManager snapshotManager) {
      _snapshotManager = snapshotManager;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDirectoryEntriesRequest)typedRequest;

      var projectPath = new FullPath(request.ProjectPath);
      var project = _snapshotManager.CurrentSnapshot.ProjectRoots
        .FirstOrDefault(x => x.Project.RootPath.Equals(projectPath));

      return new GetDirectoryEntriesResponse {
        DirectoryEntry = CreateDirectoryEntry(project, request.DirectoryRelativePath)
      };
    }

    public static DirectoryEntry CreateDirectoryEntry(ProjectRootSnapshot project, string relativePath) {
      var directorySnapshot = FindDirectorySnapshot(project, relativePath);
      if (directorySnapshot == null) {
        return null;
      }

      return new DirectoryEntry {
        Name = directorySnapshot.DirectoryName.RelativePath.Value,
        Entries = BuildEntries(directorySnapshot),
      };
    }

    private static DirectorySnapshot FindDirectorySnapshot(ProjectRootSnapshot project, string relativePath) {
      if (project == null) {
        return null;
      }

      var entry = project.Directory;
      foreach (var name in PathHelpers.SplitPath(relativePath)) {
        var child = entry.ChildDirectories.FirstOrDefault(x =>
          SystemPathComparer.EqualsNames(x.DirectoryName.Name, name));
        if (child == null) {
          entry = null;
          break;
        }

        entry = child;
      }

      return entry;
    }

    private static List<FileSystemEntry> BuildEntries(DirectorySnapshot entry) {
      var d = entry.ChildDirectories.Select(x => new DirectoryEntry {
        Name = x.DirectoryName.Name
      }).Cast<FileSystemEntry>();

      var f = entry.ChildFiles.Select(x => new FileEntry {
        Name = x.Name
      });

      return d.Concat(f).ToList();
    }
  }
}