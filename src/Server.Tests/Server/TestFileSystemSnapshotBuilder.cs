// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects.ProjectFile;
using VsChromium.Server.Threads;
using VsChromium.Tests.Mocks;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileSystemSnapshotBuilder {
    [TestMethod]
    public void ChildrenAreSortedWithTheNameComparer() {
      // Behavior change 11 (see PORTING_PRINCIPLES.md): file and directory names were sorted with
      // the culture sensitive comparer ("a_b" before "ab"), they are now sorted like after an
      // incremental update, with the ordinal (case-insensitive on Windows/macOS) name comparer.
      var fileSystem = new FileSystemMock();
      var root = fileSystem.AddDirectory(@"d:\foo");
      root.AddFile(ConfigurationFileNames.ProjectFileName, "");
      foreach (var name in new[] { "ab.txt", "a_b.txt", "B.txt", "a-c.txt" }) {
        root.AddFile(name, "text");
      }
      foreach (var name in new[] { "xy", "x_y", "X-z" }) {
        root.AddDirectory(name);
      }

      var project = new ProjectFileDiscoveryProvider(fileSystem).GetProjectFromAnyPath(new FullPath(@"d:\foo\ab.txt"));
      using var workerPool = new IndexingWorkerPool();
      var snapshot = new FileSystemSnapshotBuilder(fileSystem, new NullProgressTrackerFactory(), workerPool)
        .Compute(new FileSystemNameFactory(), null, null, new[] { project }, 1, CancellationToken.None);

      var directory = snapshot.ProjectRoots.Single().Directory;
      var fileNames = directory.ChildFiles.Select(x => x.Name).ToList();
      CollectionAssert.AreEqual(fileNames.OrderBy(x => x, SystemPathComparer.NameComparer).ToList(), fileNames);
      var directoryNames = directory.ChildDirectories.Select(x => x.DirectoryName.Name).ToList();
      CollectionAssert.AreEqual(directoryNames.OrderBy(x => x, SystemPathComparer.NameComparer).ToList(), directoryNames);
    }

    [TestMethod]
    public void InternedStringsAreReleasedAfterABuild() {
      var fileSystem = new FileSystemMock();
      var root = fileSystem.AddDirectory(@"d:\foo");
      root.AddFile(ConfigurationFileNames.ProjectFileName, "");

      var project = new ProjectFileDiscoveryProvider(fileSystem).GetProjectFromAnyPath(new FullPath(@"d:\foo"));
      var nameFactory = new FileSystemNameFactory();
      // Names created before the build (e.g. lookups) are interned too.
      nameFactory.CreateFileName(nameFactory.CreateAbsoluteDirectoryName(new FullPath(@"d:\foo")), "x.txt");
      Assert.AreEqual(1, nameFactory.InternedStringCount);
      using var workerPool = new IndexingWorkerPool();
      var snapshot = new FileSystemSnapshotBuilder(fileSystem, new NullProgressTrackerFactory(), workerPool)
        .Compute(nameFactory, null, null, new[] { project }, 1, CancellationToken.None);

      Assert.AreEqual(1, snapshot.ProjectRoots.Count);
      Assert.AreEqual(0, nameFactory.InternedStringCount, "The intern tables are only needed during a build");
    }

    [TestMethod]
    public void TheScanRecordsTheLastWriteTimeAndSizeOfFiles() {
      var root = Path.Combine(Path.GetTempPath(), "VsChromiumScanMetadataTest-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(root);
      try {
        File.WriteAllText(Path.Combine(root, ConfigurationFileNames.ProjectFileName), "[SourceExplorer.ignore]\n");
        var b = WriteFile(root, "b.txt", "text of b", new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        WriteFile(root, "a.txt", "a", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var fileSystem = new PortableFileSystem();
        var project = new ProjectFileDiscoveryProvider(fileSystem).GetProjectFromAnyPath(new FullPath(Path.Combine(root, "a.txt")));
        using var workerPool = new IndexingWorkerPool();
        var nameFactory = new FileSystemNameFactory();
        var builder = new FileSystemSnapshotBuilder(fileSystem, new NullProgressTrackerFactory(), workerPool);
        var snapshot = builder.Compute(nameFactory, null, null, new[] { project }, 1, CancellationToken.None);

        // From the directory enumeration, in the order of the files (sorted by name).
        var metadata = Metadata(snapshot.ProjectRoots.Single().Directory);
        Assert.AreEqual(b, metadata["b.txt"]);
        Assert.AreEqual(1, metadata["a.txt"].Length);

        // Incremental update: created files get the metadata of the file system, the metadata of
        // the other files is kept.
        var c = WriteFile(root, "c.txt", "text of c!", new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));
        var changes = new FullPathChanges(new[] { new PathChangeEntry(new FullPath(root), new RelativePath("c.txt"), PathChangeKind.Created, PathKind.File) });
        snapshot = builder.Compute(nameFactory, snapshot, changes, new[] { project }, 2, CancellationToken.None);
        var directory = snapshot.ProjectRoots.Single().Directory;
        metadata = Metadata(directory);
        Assert.AreEqual(c, metadata["c.txt"]);
        Assert.AreEqual(b, metadata["b.txt"]);
        var names = directory.ChildFiles.Select(x => x.Name).ToList();
        CollectionAssert.AreEqual(names.OrderBy(x => x, SystemPathComparer.NameComparer).ToList(), names);
      }
      finally {
        Directory.Delete(root, recursive: true);
      }
    }

    private static FileMetadata WriteFile(string directory, string name, string text, DateTime lastWriteTimeUtc) {
      var path = Path.Combine(directory, name);
      File.WriteAllText(path, text);
      File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
      return new FileMetadata(lastWriteTimeUtc, text.Length);
    }

    private static Dictionary<string, FileMetadata> Metadata(DirectorySnapshot directory) {
      var result = new Dictionary<string, FileMetadata>();
      for (var i = 0; i < directory.ChildFiles.Count; i++) {
        result.Add(directory.ChildFiles[i].Name, directory.ChildFileMetadata[i]);
      }
      return result;
    }

    private class NullProgressTrackerFactory : IProgressTrackerFactory {
      public IProgressTracker CreateTracker(int totalStepCount) => new NullProgressTracker();
      public IProgressTracker CreateIndeterminateTracker() => new NullProgressTracker();
    }

    private class NullProgressTracker : IProgressTracker {
      public bool Step() => false;
      public void DisplayProgress(DisplayProgressCallback displayProgressCallback) { }
      public void Dispose() { }
    }
  }
}
