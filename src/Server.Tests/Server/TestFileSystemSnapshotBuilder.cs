// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects.ProjectFile;
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
      var snapshot = new FileSystemSnapshotBuilder(fileSystem, new NullProgressTrackerFactory())
        .Compute(new FileSystemNameFactory(), null, null, new[] { project }, 1, CancellationToken.None);

      var directory = snapshot.ProjectRoots.Single().Directory;
      var fileNames = directory.ChildFiles.Select(x => x.Name).ToList();
      CollectionAssert.AreEqual(fileNames.OrderBy(x => x, SystemPathComparer.NameComparer).ToList(), fileNames);
      var directoryNames = directory.ChildDirectories.Select(x => x.DirectoryName.Name).ToList();
      CollectionAssert.AreEqual(directoryNames.OrderBy(x => x, SystemPathComparer.NameComparer).ToList(), directoryNames);
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
