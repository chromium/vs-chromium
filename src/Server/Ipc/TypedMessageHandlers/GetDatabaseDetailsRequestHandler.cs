// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetDatabaseDetailsRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public GetDatabaseDetailsRequestHandler(IFileSystemSnapshotManager snapshotManager, ISearchEngine searchEngine) {
      _snapshotManager = snapshotManager;
      _searchEngine = searchEngine;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDatabaseDetailsRequest) typedRequest;
      request.MaxFilesByExtensionDetailsCount = Math.Min(request.MaxFilesByExtensionDetailsCount, int.MaxValue);
      request.MaxLargeFilesDetailsCount = Math.Min(request.MaxLargeFilesDetailsCount, int.MaxValue);

      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      return new GetDatabaseDetailsResponse {
        Projects = CreateProjectsDetails(request, snapshot, database).ToList()
      };
    }

    private IEnumerable<ProjectDetails> CreateProjectsDetails(GetDatabaseDetailsRequest request,
      FileSystemSnapshot snapshot, IFileDatabaseSnapshot database) {
      return snapshot.ProjectRoots.Select(project => CreateProjectDetails(database, project, request));
    }

    private ProjectDetails CreateProjectDetails(IFileDatabaseSnapshot database, ProjectRootSnapshot project,
      GetDatabaseDetailsRequest request) {
      return new ProjectDetails {
        RootPath = project.Project.RootPath.Value,
        DirectoryDetails = GetDirectoryDetailsRequestHandler.CreateDirectoryDetails(database, project,
          project.Directory,
          request.MaxFilesByExtensionDetailsCount, request.MaxLargeFilesDetailsCount)
      };
    }
  }
}