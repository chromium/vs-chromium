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
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
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
      var request = (GetDatabaseDetailsRequest)typedRequest;
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
      return snapshot.ProjectRoots.Select(project => CreateProjectDetails(request, project, database));
    }

    private ProjectDetails CreateProjectDetails(GetDatabaseDetailsRequest request, ProjectRootSnapshot project,
      IFileDatabaseSnapshot database) {
      var fileDatabse = (FileDatabaseSnapshot) database;

      var projectFileNames = fileDatabse.FileNames
        .Where(x => x.BasePath.Equals(project.Project.RootPath))
        .ToHashSet();

      var projectFileContents = fileDatabse.Files.Values
        .Where(x => projectFileNames.Contains(x.FileName))
        .Where(x => x.HasContents())
        .ToList();

      var directoryDetails = new DirectoryDetails {
        Path = project.Project.RootPath.Value,
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
          .TakeOrderByDescending(request.MaxFilesByExtensionDetailsCount, x => x.FilesByteLength)
          .ToList(),
        LargeFilesDetails = projectFileContents
          .TakeOrderByDescending(request.MaxLargeFilesDetailsCount, x => x.Contents.ByteLength)
          .Select(x => new LargeFileDetails {
            RelativePath = x.FileName.RelativePath.Value,
            ByteLength = x.Contents.ByteLength,
          })
          .ToList()
      };
      return new ProjectDetails {
        RootPath = project.Project.RootPath.Value,
        DirectoryDetails = directoryDetails
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