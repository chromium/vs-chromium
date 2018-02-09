// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Configuration;
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
        DirectoryDetails = CreateDirectoryDetails(database, projectSnapshot, directorySnaphot,
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
      ProjectRootSnapshot projectSnapshot,
      DirectorySnapshot baseDirectory, int maxFilesByExtensionDetailsCount, int maxLargeFilesDetailsCount) {
      var directoryPath = baseDirectory.DirectoryName.FullPath;
      var fileDatabse = (FileDatabaseSnapshot)database;

      var directoryFileNames = fileDatabse.FileNames
        .Where(x => directoryPath.ContainsPath(x.FullPath))
        .ToHashSet();

      var directoryFiles = fileDatabse.Files.Values
        .Where(x => directoryFileNames.Contains(x.FileName))
        .ToList();

      var searchableFiles = directoryFiles
        .Where(x => x.HasContents())
        .ToList();

      var binaryFiles = directoryFiles
        .Where(x => (x.Contents is BinaryFileContents) && (((BinaryFileContents)x.Contents).BinaryFileSize > 0))
        .ToList();

      return new DirectoryDetails {
        Path = directoryPath.Value,

        DirectoryCount = FileSystemSnapshotManager.CountDirectoryEntries(baseDirectory),

        FileCount = FileSystemSnapshotManager.CountFileEntries(baseDirectory),

        SearchableFilesCount = searchableFiles.Count,

        SearchableFilesByteLength = searchableFiles.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength),

        BinaryFilesCount = binaryFiles.Count,

        BinaryFilesByteLength = binaryFiles.Aggregate(0L, (acc, x) => acc + ((BinaryFileContents)x.Contents).BinaryFileSize),

        SearchableFilesByExtensionDetails = searchableFiles
          .GroupBy(x => GetFileExtension(x.FileName))
          .Select(g => new FileByExtensionDetails {
            FileExtension = g.Key,
            FileCount = g.Count(),
            FilesByteLength = g.Aggregate(0L, (acc, x) => acc + x.Contents.ByteLength)
          })
          .OrderByDescendingThenTake(maxFilesByExtensionDetailsCount, x => x.FilesByteLength)
          .ToList(),

        LargeSearchableFilesDetails = searchableFiles
          .OrderByDescendingThenTake(maxLargeFilesDetailsCount, x => x.Contents.ByteLength)
          .Select(x => new LargeFileDetails {
            RelativePath = GetRelativePath(directoryPath, x.FileName.FullPath),
            ByteLength = x.Contents.ByteLength
          })
          .ToList(),

        LargeBinaryFilesDetails = binaryFiles
          .OrderByDescendingThenTake(maxLargeFilesDetailsCount, x => ((BinaryFileContents)x.Contents).BinaryFileSize)
          .Select(x => new LargeFileDetails {
            RelativePath = GetRelativePath(directoryPath, x.FileName.FullPath),
            ByteLength = ((BinaryFileContents)x.Contents).BinaryFileSize
          })
          .ToList(),

        ProjectConfigurationDetails = CreateProjectConfigurationDetails(projectSnapshot)
      };
    }

    private static ProjectConfigurationDetails CreateProjectConfigurationDetails(ProjectRootSnapshot project) {
      return new ProjectConfigurationDetails {
        IgnorePathsSection = CreateSectionDetails(project.Project.IgnorePathsConfiguration),
        IgnoreSearchableFilesSection = CreateSectionDetails(project.Project.IgnoreSearchableFilesConfiguration),
        IncludeSearchableFilesSection = CreateSectionDetails(project.Project.IncludeSearchableFilesConfiguration)
      };
    }

    private static ProjectConfigurationSectionDetails CreateSectionDetails(IConfigurationSectionContents section) {
      return new ProjectConfigurationSectionDetails {
        ContainingFilePath = section.ContainingFilePath.Value,
        Name = section.Name,
        Contents = section.Contents.Aggregate((acc, s1) => acc + "\r\n" + s1)
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