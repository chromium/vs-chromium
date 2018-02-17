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
  public class GetFileSystemTreeRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;

    [ImportingConstructor]
    public GetFileSystemTreeRequestHandler(IFileSystemSnapshotManager snapshotManager) {
      _snapshotManager = snapshotManager;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      return new GetFileSystemTreeResponse {
        Tree = _snapshotManager.CurrentSnapshot.ToIpcCompactFileSystemTree()
      };
    }
  }

  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetDirectoryEntriesRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;

    [ImportingConstructor]
    public GetDirectoryEntriesRequestHandler(IFileSystemSnapshotManager snapshotManager) {
      _snapshotManager = snapshotManager;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDirectoryEntriesRequest) typedRequest;

      var projectPath = new FullPath(request.ProjectPath);
      var project = _snapshotManager.CurrentSnapshot.ProjectRoots
        .FirstOrDefault(x => x.Project.RootPath.Equals(projectPath));

      if (project == null) {
        return new GetDirectoryEntriesResponse {
          DirectoryEntry = null
        };
      }

      var entry = project.Directory;
      foreach (var name in PathHelpers.SplitPath(request.DirectoryRelativePath)) {
        var child = entry.ChildDirectories.FirstOrDefault(x =>
          SystemPathComparer.EqualsNames(x.DirectoryName.Name, name));
        if (child == null) {
          return new GetDirectoryEntriesResponse {
            DirectoryEntry = null
          };
        }

        entry = child;
      }

      return new GetDirectoryEntriesResponse {
        DirectoryEntry = new DirectoryEntry {
           Name = entry.DirectoryName.RelativePath.Value,
          Entries = BuildEntries(entry),
        } 
      };
    }

    private List<FileSystemEntry> BuildEntries(DirectorySnapshot entry) {
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