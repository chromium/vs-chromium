// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
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
      var projectSnapshot = snapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.ContainsPath(directoryPath));
      if (projectSnapshot == null) {
        throw new RecoverableErrorException($"Directory \"{request.Path}\" not found in index");
      }
      var directorySnaphot = FindDirectorySnapshot(directoryPath, projectSnapshot);
      if (directorySnaphot == null) {
        throw new RecoverableErrorException($"Directory \"{request.Path}\" not found in index");
      }

      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      return new GetDirectoryDetailsResponse {
        DirectoryDetails = CreateDirectoryDetails(database, directorySnaphot,
          request.MaxFilesByExtensionDetailsCount, request.MaxLargeFilesDetailsCount)
      };
    }

    private DirectorySnapshot FindDirectorySnapshot(FullPath directoryPath, ProjectRootSnapshot projectSnapshot) {
      var splitPath = PathHelpers.SplitPrefix(directoryPath.Value, projectSnapshot.Project.RootPath.Value);
      var current = projectSnapshot.Directory;
      foreach (var name in PathHelpers.SplitPath(splitPath.Suffix)) {
        current = current.ChildDirectories
          .FirstOrDefault(x => SystemPathComparer.EqualsNames(name, x.DirectoryName.Name));
        if (current == null) {
          return null;
        }
      }
      return current;
    }

    public static DirectoryDetails CreateDirectoryDetails(IFileDatabaseSnapshot database,
      DirectorySnapshot baseDirectory, int maxFilesByExtensionDetailsCount, int maxLargeFilesDetailsCount) {
      var directoryPath = baseDirectory.DirectoryName.FullPath;
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
        DirectoryCount = FileSystemSnapshotManager.CountDirectoryEntries(baseDirectory),
        FileCount = FileSystemSnapshotManager.CountFileEntries(baseDirectory),
        SearchableFileCount = projectFileContents.Count,
        SearchableFileByteLength = projectFileContents.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength),
        SearchableFilesByExtensionDetails = projectFileContents
          .GroupBy(x => GetFileExtension(x.FileName))
          .Select(g => new FileByExtensionDetails {
            FileExtension = g.Key,
            FileCount = g.Count(),
            FilesByteLength = g.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength)
          })
          .TakeOrderByDescending(maxFilesByExtensionDetailsCount, x => x.FilesByteLength)
          .ToList(),
        LargeSearchableFilesDetails = projectFileContents
          .TakeOrderByDescending(maxLargeFilesDetailsCount, x => x.Contents.ByteLength)
          .Select(x => new LargeFileDetails {
            RelativePath = GetRelativePath(directoryPath, x.FileName.FullPath),
            ByteLength = x.Contents.ByteLength
          })
          .ToList(),
        LargeBinaryFilesDetails = projectFiles
          .Where(x => (x.Contents is BinaryFileContents) && (((BinaryFileContents)x.Contents).BinaryFileSize > 0))
          .TakeOrderByDescending(maxLargeFilesDetailsCount, x => ((BinaryFileContents)x.Contents).BinaryFileSize)
          .Select(x => new LargeFileDetails {
            RelativePath = GetRelativePath(directoryPath, x.FileName.FullPath),
            ByteLength = ((BinaryFileContents)x.Contents).BinaryFileSize
          })
          .ToList()
      };
    }

    private static string GetRelativePath(FullPath parentPath, FullPath path) {
      return PathHelpers.SplitPrefix(path.Value, parentPath.Value).Suffix;
    }

    private static string GetFileExtension(FileName fileName) {
      var ext = PathHelpers.GetExtension(fileName.Name);
      if (string.IsNullOrEmpty(ext)) {
        return fileName.Name;
      }
      return ext;
    }
  }
}