// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Server.Projects.Chromium;
using VsChromium.Tests.Mocks;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestChromiumFileDiscoveryProvider {

    private string LoadConfigFile(string name) {
      var localFileSystem = new FileSystem();
      var path = new FullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
        .Combine(new RelativePath(ConfigurationDirectoryNames.LocalInstallConfigurationDirectoryName));
      var filePath = path.Combine(new RelativePath(name));
      if (localFileSystem.FileExists(filePath)) {
        return localFileSystem.ReadText(filePath);
      }
      throw Invariants.Fail("File not found");
    }

    private void AddConfigFile(FileSystemMock fileSystem, string name) {
      var path = new FullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        .Combine(new RelativePath(ConfigurationDirectoryNames.LocalUserConfigurationDirectoryName));
      var cfgdir = fileSystem.AddDirectories(path.Value);
      cfgdir.AddFile(name, LoadConfigFile(name));
    }

    [TestMethod]
    public void DiscoverProjectFileInSubFolderWorks() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddDirectory(@"bar")
        .AddDirectory(@"base").Parent
        .AddDirectory(@"chrome").Parent
        .AddDirectory(@"content").Parent
        .AddFile(@"presubmit.py", "hello").Parent
        .AddFile(@"test.txt", "some text content");

      // Add configuration directory
      AddConfigFile(fileSystem, ConfigurationFileNames.ChromiumEnlistmentDetectionPatterns);
      AddConfigFile(fileSystem, ConfigurationSectionNames.SourceExplorerIgnoreObsolete);
      AddConfigFile(fileSystem, ConfigurationSectionNames.SearchableFilesIgnore);
      AddConfigFile(fileSystem, ConfigurationSectionNames.SearchableFilesInclude);

      var locator = new ConfigurationFileLocator(fileSystem);
      var provider = new ChromiumProjectDiscoveryProvider(locator, fileSystem);
      var project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar\test.txt"));
      Assert.IsNotNull(project);
      Assert.AreEqual(@"d:\foo\bar", project.RootPath.Value);
      Assert.IsTrue(project.FileFilter.Include(new RelativePath("none.txt")));
      Assert.IsTrue(project.DirectoryFilter.Include(new RelativePath("none")));
      Assert.IsTrue(project.SearchableFilesFilter.Include(new RelativePath("source.cpp")));


      project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo\bar"));
      Assert.IsNotNull(project);
      Assert.AreEqual(@"d:\foo\bar", project.RootPath.Value);

      project = provider.GetProjectFromAnyPath(new FullPath(@"d:\foo"));
      Assert.IsNull(project);
    }
  }
}
