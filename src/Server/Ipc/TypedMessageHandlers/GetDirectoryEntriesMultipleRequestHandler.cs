// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetDirectoryEntriesMultipleRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;

    [ImportingConstructor]
    public GetDirectoryEntriesMultipleRequestHandler(IFileSystemSnapshotManager snapshotManager) {
      _snapshotManager = snapshotManager;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDirectoryEntriesMultipleRequest)typedRequest;

      var projectPath = new FullPath(request.ProjectPath);
      var project = _snapshotManager.CurrentSnapshot.ProjectRoots
        .FirstOrDefault(x => x.Project.RootPath.Equals(projectPath));

      return new GetDirectoryEntriesMultipleResponse {
        DirectoryEntries = request.RelativePathList
          .Select(relativePath => MapDirectoryEntry(project, relativePath))
          .ToList()
      };
    }

    private static OptionalDirectoryEntry MapDirectoryEntry(ProjectRootSnapshot project, string relativePath) {
      var directoryEntry = GetDirectoryEntriesRequestHandler.CreateDirectoryEntry(project, relativePath);
      return directoryEntry == null
        ? new OptionalDirectoryEntry { HasValue = false }
        : new OptionalDirectoryEntry { HasValue = true, Value = directoryEntry };
    }
  }
}