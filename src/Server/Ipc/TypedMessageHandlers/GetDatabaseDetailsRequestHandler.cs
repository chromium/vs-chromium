// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetDatabaseDetailsRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;
    private readonly ISearchEngine _searchEngine;
    private readonly IIndexingServer _indexingServer;

    [ImportingConstructor]
    public GetDatabaseDetailsRequestHandler(IFileSystemSnapshotManager snapshotManager, ISearchEngine searchEngine, IIndexingServer indexingServer) {
      _snapshotManager = snapshotManager;
      _searchEngine = searchEngine;
      _indexingServer = indexingServer;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDatabaseDetailsRequest)typedRequest;

      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      return new GetDatabaseDetailsResponse {
        Projects = CreateProjectsDetails(snapshot, database).ToList()
      };
    }

    private IEnumerable<ProjectDetails> CreateProjectsDetails(FileSystemSnapshot snapshot, IFileDatabaseSnapshot database) {
      return snapshot.ProjectRoots.Select(project => CreateProjectDetails(project, database));
    }

    private ProjectDetails CreateProjectDetails(ProjectRootSnapshot project, IFileDatabaseSnapshot database) {
      var fileDatabse = (FileDatabaseSnapshot) database;

      var projectFileNames = fileDatabse.FileNames
        .Where(x => x.BasePath.Equals(project.Project.RootPath))
        .ToHashSet();

      var projectFileContents = fileDatabse.Files.Values
        .Where(x => projectFileNames.Contains(x.FileName))
        .Where(x => x.HasContents())
        .ToList();

      return new ProjectDetails() {
        RootPath = project.Project.RootPath.Value,
        DirectoryCount = FileSystemSnapshotManager.CountDirectoryEntries(project.Directory), 
        FileCount = FileSystemSnapshotManager.CountFileEntries(project.Directory),
        SearchableFileCount = projectFileContents.Count,
        SearchableFileByteLength = projectFileContents.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength),
        FilesByExtensionDetails = projectFileContents
          .GroupBy(x => GetFileExtension(x.FileName))
          .Select(g => new FileByExtensionDetails {
            FileExtension = g.Key,
            FileCount = g.Count(),
            FilesByteLength = g.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength)
          })
          .OrderByDescending(x => x.FilesByteLength)
          .ToList()
      };
    }

    private string GetFileExtension(FileName fileName) {
      var ext = PathHelpers.GetExtension(fileName.Name);
      if (string.IsNullOrEmpty(ext)) {
        return fileName.Name;
      }
      return ext;
    }
  }
}