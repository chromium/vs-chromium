// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.FileNames;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemSnapshot {
  [Export(typeof(IFileSystemSnapshotBuilder))]
  public class FileSystemSnapshotBuilder : IFileSystemSnapshotBuilder {
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;

    [ImportingConstructor]
    public FileSystemSnapshotBuilder(
      IProjectDiscovery projectDiscovery,
      IProgressTrackerFactory progressTrackerFactory,
      IFileSystemNameFactory fileSystemNameFactory) {
      _projectDiscovery = projectDiscovery;
      _progressTrackerFactory = progressTrackerFactory;
      _fileSystemNameFactory = fileSystemNameFactory;
    }

    public FileSystemTreeSnapshot Compute(IEnumerable<FullPathName> filenames, int verion) {
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        var projectRoots =
          filenames
            .Select(filename => _projectDiscovery.GetProject(filename))
            .Where(project => project != null)
            .Distinct(new ProjectPathComparer())
            .Select(project => new ProjectRootSnapshot(project, ProcessProject(project, progress)))
            .OrderBy(projectRoot => projectRoot.Directory.DirectoryName)
            .ToReadOnlyCollection();

        return new FileSystemTreeSnapshot(verion, projectRoots);
      }
    }

    private DirectorySnapshot ProcessProject(IProject project, IProgressTracker progress) {
      var projectPath = _fileSystemNameFactory.CombineDirectoryNames(_fileSystemNameFactory.Root, project.RootPath);

      // List [DirectoryName, FileNames]
      var directories = TraverseFileSystem(project, projectPath)
        .AsParallel()
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(traversedDirectoryEntry => {
          var directoryName = traversedDirectoryEntry.DirectoryName;
          progress.Step(
            (i, n) =>
            string.Format("Traversing directory: {0}",
                          PathHelpers.PathCombine(project.RootPath, directoryName.RelativePathName.RelativeName)));
          var entries = traversedDirectoryEntry.ChildrenNames
            .Where(childFilename => project.FileFilter.Include(childFilename.RelativePathName.RelativeName))
            .ToList();

          return Tuple.Create(directoryName, entries);
        })
        // We sort entries by directory name *descending* to make sure we process
        // directories bottom up, so that we know
        // 1) it is safe to skip DirectoryEntry instances where "Entries.Count" == 0,
        // 2) we create instances of child directories before their parent.
        .OrderByDescending(x => x.Item1)
        .ToList();

      // Build map from parent directory -> list of child directories
      var direcoryNameToChildDirectoryNames =
        directories.ToDictionary(x => x.Item1, x => new List<DirectoryName>());
      directories.ForAll(x => {
        var directoryName = x.Item1;
        if (directoryName.IsRoot || directoryName.IsAbsoluteName)
          return;

        direcoryNameToChildDirectoryNames[directoryName.Parent].Add(directoryName);
      });

      // Build directory entries, using a intermediate map to build parent => children relation.
      var directoryNameToEntry = new Dictionary<DirectoryName, DirectorySnapshot>();
      var directoryEntries = directories.Select(tuple => {
        var directoryName = tuple.Item1;
        var childFileEntries = tuple.Item2;

        var childDirectoryEntries = direcoryNameToChildDirectoryNames[directoryName]
          .Select(x => directoryNameToEntry[x])
          .OrderBy(x => x.DirectoryName)
          .ToReadOnlyCollection();

        var childFilenames = childFileEntries
          .OrderBy(x => x)
          .ToReadOnlyCollection();

        var result = new DirectorySnapshot(directoryName, childDirectoryEntries, childFilenames);
        directoryNameToEntry[directoryName] = result;
        return result;
      })
      .ToList();

      return directoryEntries.Single(x => x.DirectoryName.Equals(projectPath));
    }

    /// <summary>
    /// Enumerate directories and files under the project path of |projet|.
    /// </summary>
    private IEnumerable<TraversedDirectoryEntry> TraverseFileSystem(IProject project, DirectoryName projectPath) {
      Debug.Assert(!projectPath.IsRoot);
      Debug.Assert(projectPath.IsAbsoluteName);
      var stack = new Stack<DirectoryName>();
      stack.Push(projectPath);
      while (stack.Count > 0) {
        var head = stack.Pop();
        if (head.IsAbsoluteName || project.DirectoryFilter.Include(head.RelativePathName.RelativeName)) {
          RelativePathName[] childDirectories;
          RelativePathName[] childFiles;
          head.RelativePathName.GetFileSystemEntries(project.RootPath, out childDirectories, out childFiles);
          // Note: Use "for" loop to avoid memory allocations.
          for (var i = 0; i < childDirectories.Length; i++) {
            stack.Push(_fileSystemNameFactory.CreateDirectoryName(head, childDirectories[i]));
          }
          // Note: Use "for" loop to avoid memory allocations.
          var childFilenames = new FileName[childFiles.Length];
          for (var i = 0; i < childFiles.Length; i++) {
            childFilenames[i] = _fileSystemNameFactory.CreateFileName(head, childFiles[i]);
          }
          yield return new TraversedDirectoryEntry(head, childFilenames);
        }
      }
    }

    private struct TraversedDirectoryEntry {
      private readonly DirectoryName _directoryName;
      private readonly FileName[] _childrenNames;

      public TraversedDirectoryEntry(DirectoryName directoryName, FileName[] childrenNames) {
        _directoryName = directoryName;
        _childrenNames = childrenNames;
      }

      public DirectoryName DirectoryName { get { return _directoryName; } }
      public FileName[] ChildrenNames { get { return _childrenNames; } }
    }
  }
}
