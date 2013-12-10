// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Linq;
using VsChromiumPackage.Server;

namespace VsChromiumTests.Server {
  [TestClass]
  public class TestSearchFileContents : TestServerBase {
    [TestMethod]
    public void SingleOccurrenceWorks() {
      const string searchPattern = "Test directory looking like";

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          GetFileSystemTreeFromServer(server, testFile);

          VerifySearchFileContentsResponse(server, searchPattern, Options.MatchCase, testFile.Directory, 1);

          var searchPatternLower = searchPattern.ToLowerInvariant();
          VerifySearchFileContentsResponse(server, searchPatternLower, Options.None, testFile.Directory, 1);
        }
      }
    }

    [TestMethod]
    public void MultipleOccurrenceWorks() {
      const string searchPattern = "Nothing here. Just making sure the directory exists.";

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          GetFileSystemTreeFromServer(server, testFile);

          VerifySearchFileContentsResponse(server, searchPattern, Options.MatchCase, testFile.Directory, 3);

          var searchPatternLower = searchPattern.ToLowerInvariant();
          VerifySearchFileContentsResponse(server, searchPatternLower, Options.None, testFile.Directory, 3);
        }
      }
    }

    [Flags]
    private enum Options {
      None = 0,
      MatchCase = 1,
    }

    private static void VerifySearchFileContentsResponse(
      ITypedRequestProcessProxy server,
      string searchPattern,
      Options options,
      DirectoryInfo chromiumDirectory,
      int occurrenceCount) {
      var response = SendRequest<SearchFileContentsResponse>(server, new SearchFileContentsRequest {
        SearchParams = new SearchParams {
          SearchString = searchPattern,
          MaxResults = 2000,
          MatchCase = ((options & Options.MatchCase) != 0)
        }
      }, ServerResponseTimeout)();
      Assert.IsNotNull(response, "Server did not respond within timeout.");
      Assert.IsNotNull(response.SearchResults);
      Assert.IsNotNull(response.SearchResults.Entries);

      Assert.AreEqual(1, response.SearchResults.Entries.Count);
      var chromiumEntry = response.SearchResults.Entries[0] as DirectoryEntry;
      Assert.IsNotNull(chromiumEntry);
      Assert.AreEqual(chromiumDirectory.FullName, chromiumEntry.Name);

      chromiumEntry.Entries.ForAll(x => {
        Debug.WriteLine(string.Format("File name: \"{0}\"", x.Name));
        Assert.IsNotNull(x.Data);
        Assert.IsTrue(x.Data is FilePositionsData);
        ((FilePositionsData)x.Data).Positions.ForEach(y => { Debug.WriteLine(string.Format("   Text position: offset={0}, length={1}", y.Position, y.Length)); });
      });
      Assert.AreEqual(occurrenceCount, chromiumEntry.Entries.Count);
    }
  }
}
