// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Server.Projects.ProjectFile;
using VsChromium.Tests.Mocks;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestProjectFileDiscoveryProvider {
    [TestMethod]
    public void DiscoverProjectFileInSubFolderWorks() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddDirectory(@"bar")
        .AddFile(@"test.txt", "some text content")
        .Parent
        .AddFile(ConfigurationFileNames.ProjectFileName, "invalid content");

      var provider = new ProjectFileDiscoveryProvider(fileSystem);
      var project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar\test.txt"));
      Assert.IsNotNull(project);
      Assert.AreEqual(@"d:\foo\bar", project.RootPath.Value);
      Assert.IsTrue(project.FileFilter.Include(new RelativePath("none.txt")));
      Assert.IsTrue(project.DirectoryFilter.Include(new RelativePath("none")));
      Assert.IsFalse(project.SearchableFilesFilter.Include(new RelativePath("none.txt")));
    }
    [TestMethod]
    public void DiscoverProjectFileFromProjectPathInWorks() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddDirectory(@"bar")
        .AddFile(@"test.txt", "some text content")
        .Parent
        .AddFile(ConfigurationFileNames.ProjectFileName, "invalid content");

      var provider = new ProjectFileDiscoveryProvider(fileSystem);
      var project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar"));
      Assert.IsNotNull(project);
      Assert.AreEqual(@"d:\foo\bar", project.RootPath.Value);
      Assert.IsTrue(project.FileFilter.Include(new RelativePath("none.txt")));
      Assert.IsTrue(project.DirectoryFilter.Include(new RelativePath("none")));
      Assert.IsFalse(project.SearchableFilesFilter.Include(new RelativePath("none.txt")));
    }
    [TestMethod]
    public void DiscoverProjectFileFiltersWorks() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddDirectory(@"bar")
        .AddDirectory(@".git")
        .Parent
        .AddFile(@"test.txt", @"some text")
        .Parent
        .AddFile(ConfigurationFileNames.ProjectFileName,
@"
[SourceExplorer.ignore]
.git/
*.sdf
*.opensdf
*.suo

[SearchableFiles.ignore]
Binaries/
obj/
Debug/
x64/
*.png
*.ico
*.ipch
*.pch
*.dll
*.pdb

[SearchableFiles.include]
*
");

      var provider = new ProjectFileDiscoveryProvider(fileSystem);
      var project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar\test.txt"));
      Assert.IsNotNull(project);
      Assert.AreEqual(@"d:\foo\bar", project.RootPath.Value);
      Assert.IsFalse(project.FileFilter.Include(new RelativePath("test.sdf")));
      Assert.IsTrue(project.FileFilter.Include(new RelativePath(".git")));
      Assert.IsFalse(project.DirectoryFilter.Include(new RelativePath(".git")));
      Assert.IsTrue(project.SearchableFilesFilter.Include(new RelativePath("none.txt")));
      Assert.IsFalse(project.SearchableFilesFilter.Include(new RelativePath("none.png")));
    }
    [TestMethod]
    public void DiscoverProjectFileInDeepSubFolderWorks() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddDirectory(@"bar")
        .AddFile(ConfigurationFileNames.ProjectFileName, "invalid content")
        .Parent
        .AddDirectory(@"bar2")
        .AddDirectory(@"bar3")
        .AddFile(@"test.txt", "some text content");

      var provider = new ProjectFileDiscoveryProvider(fileSystem);
      var project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar\bar2\bar3\test.txt"));
      Assert.IsNotNull(project);
    }
    [TestMethod]
    public void DiscoverProjectFileFromProjectFileWorks() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddDirectory(@"bar")
        .AddFile(ConfigurationFileNames.ProjectFileName, "invalid content");

      var provider = new ProjectFileDiscoveryProvider(fileSystem);
      var project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar\" + ConfigurationFileNames.ProjectFileName));
      Assert.IsNotNull(project);
    }
    [TestMethod]
    public void DiscoverProjectFileReturnsNullIfNoProjectFileIsPresent() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddDirectory(@"bar");

      var provider = new ProjectFileDiscoveryProvider(fileSystem);
      var project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar\" + ConfigurationFileNames.ProjectFileName));
      Assert.IsNull(project);
    }
    [TestMethod]
    public void DiscoverObsoleteProjectFileWorks() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddDirectory(@"bar")
        .AddFile(@"test.txt", "some text content")
        .Parent.AddFile(ConfigurationFileNames.ProjectFileNameObsolete, "invalid content");

      var provider = new ProjectFileDiscoveryProvider(fileSystem);
      var project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar\test.txt"));
      Assert.IsNotNull(project);
    }
  }
}
