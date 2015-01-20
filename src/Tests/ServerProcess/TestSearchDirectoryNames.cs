// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.ServerProxy;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestSearchDirectoryNames : TestServerBase {
    [TestMethod]
    public void SingleOccurrenceWorks() {
      const string searchPattern = "base";
      const string directoryName = searchPattern;

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          GetFileSystemFromServer(server, testFile);

          VerifySearchDirectoryNamesResponse(server, searchPattern, testFile.Directory, directoryName, 1);
        }
      }
    }

    [TestMethod]
    public void MultipleOccurrenceWorks() {
      const string searchPattern = "test_directory";
      const string directoryName = searchPattern;

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          GetFileSystemFromServer(server, testFile);

          VerifySearchDirectoryNamesResponse(server, searchPattern, testFile.Directory, directoryName, 3);
        }
      }
    }

    [TestMethod]
    public void WildcardWorks() {
      const string searchPattern = "*est_*ectory*";
      const string directoryName = "test_directory";

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          GetFileSystemFromServer(server, testFile);

          VerifySearchDirectoryNamesResponse(server, searchPattern, testFile.Directory, directoryName, 3);
        }
      }
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
      Assert.AreEqual(occurrenceCount, chromiumEntry.Entries.Count(x => x.Name.Contains(directoryName)));
    }
  }
}
