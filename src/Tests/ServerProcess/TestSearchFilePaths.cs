// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Linq;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestSearchFilePaths : TestServerBase {
    private static CompositionContainer _container;
    private static IServerClient _server;
    private static FileInfo _testFile;

    [ClassInitialize]
    public static void Initialize(TestContext context) {
      _container = SetupMefContainer();
      _server = _container.GetExportedValue<IServerClient>();
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
      VerifySearchFilePathsResponse(_server, _testFile.Name, _testFile.Directory, _testFile.Name, 1);
    }

    [TestMethod]
    public void MultipleOccurrenceWorks() {
      const string fileName = "file_present_three_times.txt";
      const string searchPattern = "file_present_three_times.txt";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 3);
    }

    [TestMethod]
    public void WildcardWorks() {
      const string fileName = "file_present_three_times.txt";
      const string searchPattern = "file_present_*_times.*";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 3);
    }

    [TestMethod]
    public void DoubleQuotesWorks() {
      const string fileName = "";
      const string searchPattern = "\"present_three_times.txt\"";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 0);
    }

    [TestMethod]
    public void ImplicitWildcardWorks() {
      const string fileName = "";
      const string searchPattern = "readme";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 2);
    }

    [TestMethod]
    public void ImplicitWildcardAndExlusionWorks() {
      const string fileName = "";
      const string searchPattern = "-readme";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 9);
    }

    [TestMethod]
    public void DoubleQuotesAndExlusionWorks() {
      const string fileName = "";
      const string searchPattern = "-\"readme\"";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 11);
    }

    [TestMethod]
    public void SemiColonSeparatorWithWildcardWorks() {
      const string searchPattern = "*.txt;*.py";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 9);
    }

    [TestMethod]
    public void SemiColonSeparatorWithImplicitWildcardWorks() {
      const string searchPattern = ".txt;.py";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 9);
    }

    [TestMethod]
    public void SemiColonSeparatorSingleDoubleQuotesWorks() {
      const string searchPattern = ".txt;\".py\"";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 8);
    }

    [TestMethod]
    public void SemiColonSeparatorSingleDoubleQuotes2Works() {
      const string searchPattern = "\".txt\";.py";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 1);
    }

    [TestMethod]
    public void SemiColonSeparatorDoubleQuotesWorks() {
      const string searchPattern = "\".txt\";\".py\"";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 0);
    }

    [TestMethod]
    public void SemiColonSeparatorWithExclusionWorks() {
      const string searchPattern = ".txt;.py;-presubmit";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 8);
    }

    [TestMethod]
    public void SemiColonSeparatorWithExclusionsOnlyWorks() {
      const string searchPattern = "-presubmit;-readme";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 8);
    }

    [TestMethod]
    public void SemiColonSeparatorWithExclusionAndIncludingDoubleQuotesWorks() {
      const string searchPattern = ".txt;.py;-\"presubmit.py\"";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 8);
    }

    [TestMethod]
    public void SemiColonSeparatorWithExclusionAndExcludingDoubleQuotesWorks() {
      const string searchPattern = ".txt;.py;-\"presubmit\"";
      const string fileName = "";

      VerifySearchFilePathsResponse(_server, searchPattern, _testFile.Directory, fileName, 9);
    }

    private static void VerifySearchFilePathsResponse(
      IServerClient server,
      string searchPattern,
      DirectoryInfo chromiumDirectory,
      string expectedFileName,
      int expectedOccurrenceCount) {
      var response = SendRequest(server, client => client.SearchPathsAsync(new SearchParams {
        Query = new SearchQuery {
          Text = searchPattern,
        },
        MaxResults = 2000,
      }));
      Assert.IsNotNull(response, "Server did not respond within timeout.");
      Assert.IsNotNull(response.Projects);

      if (expectedOccurrenceCount == 0) {
        Assert.AreEqual(0, response.Projects.Count);
      } else {
        Assert.AreEqual(1, response.Projects.Count);
        var chromiumEntry = response.Projects[0];
        Assert.AreEqual(chromiumDirectory.FullName, chromiumEntry.RootPath);

        chromiumEntry.RelativePaths.ForAll(x => Debug.WriteLine(string.Format("File name: \"{0}\"", x)));
        Assert.AreEqual(expectedOccurrenceCount, chromiumEntry.RelativePaths.Count);
        Assert.AreEqual(expectedOccurrenceCount, response.HitCount);
        if (expectedFileName != "") {
          Assert.AreEqual(expectedOccurrenceCount, chromiumEntry.RelativePaths.Count(x => Path.GetFileName(x) == expectedFileName));
        }
      }
    }
  }
}
