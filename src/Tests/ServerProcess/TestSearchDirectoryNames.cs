// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.ServerProxy;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestSearchDirectoryNames : TestServerBase {
    private static CompositionContainer _container;
    private static ITypedRequestProcessProxy _server;
    private static FileInfo _testFile;

    [ClassInitialize]
    public static void Initialize(TestContext context) {
      _container = SetupMefContainer();
      _server = _container.GetExportedValue<ITypedRequestProcessProxy>();
      _testFile = Utils.GetChromiumTestEnlistmentFile();
      GetFileSystemFromServer(_server, _testFile);
    }

    [ClassCleanup]
    public static void Cleanup() {
      _server.Dispose();
      _container.Dispose();
    }

    [TestMethod]
    public void SingleOccurrenceWorks() {
      const string searchPattern = "base";
      const string directoryName = searchPattern;

      VerifySearchDirectoryNamesResponse(_server, searchPattern, _testFile.Directory, directoryName, 1);
    }

    [TestMethod]
    public void MultipleOccurrenceWorks() {
      const string searchPattern = "test_directory";
      const string directoryName = searchPattern;

      VerifySearchDirectoryNamesResponse(_server, searchPattern, _testFile.Directory, directoryName, 3);
    }

    [TestMethod]
    public void WildcardWorks() {
      const string searchPattern = "*est_*ectory*";
      const string directoryName = "test_directory";

      VerifySearchDirectoryNamesResponse(_server, searchPattern, _testFile.Directory, directoryName, 3);
    }

    [TestMethod]
    public void SemiColonSeparatorWithFullNamesWorks() {
      const string searchPattern = "content;folder";
      const string directoryName = "";

      VerifySearchDirectoryNamesResponse(_server, searchPattern, _testFile.Directory, directoryName, 4);
    }

    [TestMethod]
    public void SemiColonSeparatorWithFullNamesWorks2() {
      const string searchPattern = "content/;folder/";
      const string directoryName = "";

      VerifySearchDirectoryNamesResponse(_server, searchPattern, _testFile.Directory, directoryName, 5);
    }

    [TestMethod]
    public void SemiColonSeparatorWithPartialNamesWorks() {
      const string searchPattern = "conten;older";
      const string directoryName = "";

      VerifySearchDirectoryNamesResponse(_server, searchPattern, _testFile.Directory, directoryName, 4);
    }

    private static void VerifySearchDirectoryNamesResponse(
      ITypedRequestProcessProxy server,
      string searchPattern,
      DirectoryInfo chromiumDirectory,
      string directoryName,
      int occurrenceCount) {
      var response = SendRequest<SearchDirectoryNamesResponse>(server, new SearchDirectoryNamesRequest {
        SearchParams = new SearchParams {
          SearchString = searchPattern,
          MaxResults = 2000,
        }
      }, ServerResponseTimeout)();
      Assert.IsNotNull(response, "Server did not respond within timeout.");
      Assert.IsNotNull(response.SearchResult);
      Assert.IsNotNull(response.SearchResult.Entries);

      Assert.AreEqual(1, response.SearchResult.Entries.Count);
      var chromiumEntry = response.SearchResult.Entries[0] as DirectoryEntry;
      Assert.IsNotNull(chromiumEntry);
      Assert.AreEqual(chromiumDirectory.FullName, chromiumEntry.Name);

      chromiumEntry.Entries.ForAll(x => Debug.WriteLine(string.Format("Directory name: \"{0}\"", x.Name)));
      Assert.AreEqual(occurrenceCount, chromiumEntry.Entries.Count);
      if (directoryName != "") { 
        Assert.AreEqual(occurrenceCount, chromiumEntry.Entries.Count(x => x.Name.Contains(directoryName)));
      }
    }
  }
}
