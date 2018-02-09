// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetDirectoryDetailsRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemSnapshotManager _snapshotManager;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public GetDirectoryDetailsRequestHandler(IFileSystemSnapshotManager snapshotManager, ISearchEngine searchEngine) {
      _snapshotManager = snapshotManager;
      _searchEngine = searchEngine;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDirectoryDetailsRequest)typedRequest;
      request.MaxFilesByExtensionDetailsCount = Math.Min(request.MaxFilesByExtensionDetailsCount, int.MaxValue);
      request.MaxLargeFilesDetailsCount = Math.Min(request.MaxLargeFilesDetailsCount, int.MaxValue);

      var directoryPath = new FullPath(request.Path);
      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      var projectSnapshot = snapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.ContainsPath(directoryPath));
      return new GetDirectoryDetailsResponse {
        DirectoryDetails = CreateDirectoryDetails(request, directoryPath, projectSnapshot, database)
      };
    }

    private DirectoryDetails CreateDirectoryDetails(GetDirectoryDetailsRequest request, FullPath directoryPath,
      ProjectRootSnapshot project, IFileDatabaseSnapshot database) {
      var fileDatabse = (FileDatabaseSnapshot)database;

      var projectFileNames = fileDatabse.FileNames
        .Where(x => directoryPath.ContainsPath(x.FullPath))
        .ToHashSet();

      var projectFiles = fileDatabse.Files.Values
        .Where(x => projectFileNames.Contains(x.FileName))
        .ToList();

      var projectFileContents = projectFiles
        .Where(x => x.HasContents())
        .ToList();

      return new DirectoryDetails {
        Path = directoryPath.Value,
        DirectoryCount = FileSystemSnapshotManager.CountDirectoryEntries(project.Directory),
        FileCount = FileSystemSnapshotManager.CountFileEntries(project.Directory),
        SearchableFileCount = projectFileContents.Count,
        SearchableFileByteLength = projectFileContents.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength),
        SearchableFilesByExtensionDetails = projectFileContents
          .GroupBy(x => GetFileExtension(x.FileName))
          .Select(g => new FileByExtensionDetails {
            FileExtension = g.Key,
            FileCount = g.Count(),
            FilesByteLength = g.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength)
          })
          .TakeOrderByDescending(request.MaxFilesByExtensionDetailsCount, x => x.FilesByteLength)
          .ToList(),
        LargeSearchableFilesDetails = projectFileContents
          .TakeOrderByDescending(request.MaxLargeFilesDetailsCount, x => x.Contents.ByteLength)
          .Select(x => new LargeFileDetails {
            RelativePath = x.FileName.RelativePath.Value,
            ByteLength = x.Contents.ByteLength
          })
          .ToList(),
        LargeBinaryFilesDetails = projectFiles
          .Where(x => (x.Contents is BinaryFileContents) && (((BinaryFileContents)x.Contents).BinaryFileSize > 0))
          .TakeOrderByDescending(request.MaxLargeFilesDetailsCount, x => ((BinaryFileContents)x.Contents).BinaryFileSize)
          .Select(x => new LargeFileDetails {
            RelativePath = x.FileName.RelativePath.Value,
            ByteLength = ((BinaryFileContents)x.Contents).BinaryFileSize
          })
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