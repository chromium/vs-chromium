// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
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
      var projectFileNames = new HashSet<FileName>(database.FileNames.Where(x => x.BasePath.Equals(project.Project.RootPath)));
      var projectFileContents = database.FileContentsPieces
        .Where(x => projectFileNames.Contains(x.FileName))
        .Select(x => new FileWithContents(x.FileName, x.FileContents))
        .Distinct(new FileWithContentsComparer())
        .ToDictionary(x => x.FileName, x => x);
      return new ProjectDetails() {
        RootPath = project.Project.RootPath.Value,
        FileCount = projectFileNames.Count,
        SearchableFileCount = projectFileContents.Count,
        SearchableFileByteLength = projectFileContents.Values.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength),
        FilesByExtensionDetails = projectFileContents.Values
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

    private class FileWithContentsComparer : IEqualityComparer<FileWithContents> {
      public bool Equals(FileWithContents x, FileWithContents y) {
        return x.FileName.Equals(y.FileName);
      }

      public int GetHashCode(FileWithContents obj) {
        return obj.FileName.GetHashCode();
      }
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