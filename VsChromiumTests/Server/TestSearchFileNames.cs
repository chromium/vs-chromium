// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Linq;
using VsChromiumPackage.Server;

namespace VsChromiumTests.Server {
  [TestClass]
  public class TestSearchFileNames : TestServerBase {
    [TestMethod]
    public void SingleOccurrenceWorks() {
      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          GetFileSystemTreeFromServer(server, testFile);

          VerifySearchFileNamesResponse(server, testFile.Name, testFile.Directory, testFile.Name, 1);
        }
      }
    }

    [TestMethod]
    public void MultipleOccurrenceWorks() {
      const string fileName = "file_present_three_times.txt";
      const string searchPattern = "file_present_three_times.txt";

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          GetFileSystemTreeFromServer(server, testFile);

          VerifySearchFileNamesResponse(server, searchPattern, testFile.Directory, fileName, 3);
        }
      }
    }

    [TestMethod]
    public void WildcardWorks() {
      const string fileName = "file_present_three_times.txt";
      const string searchPattern = "file_present_*_times.*";

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          GetFileSystemTreeFromServer(server, testFile);

          VerifySearchFileNamesResponse(server, searchPattern, testFile.Directory, fileName, 3);
        }
      }
    }

    private static void VerifySearchFileNamesResponse(
      ITypedRequestProcessProxy server,
      string searchPattern,
      DirectoryInfo chromiumDirectory,
      string fileName,
      int occurrenceCount) {
      var response = SendRequest<SearchFileNamesResponse>(server, new SearchFileNamesRequest {
        SearchParams = new SearchParams {
          SearchString = searchPattern,
          MaxResults = 2000,
        }
      }, ServerResponseTimeout)();
      Assert.IsNotNull(response, "Server did not respond within timeout.");
      Assert.IsNotNull(response.FileNames);
      Assert.IsNotNull(response.FileNames.Entries);

      Assert.AreEqual(1, response.FileNames.Entries.Count);
      var chromiumEntry = response.FileNames.Entries[0] as DirectoryEntry;
      Assert.IsNotNull(chromiumEntry);
      Assert.AreEqual(chromiumDirectory.FullName, chromiumEntry.Name);

      chromiumEntry.Entries.ForAll(x => Debug.WriteLine(string.Format("File name: \"{0}\"", x.Name)));
      Assert.AreEqual(occurrenceCount, chromiumEntry.Entries.Count);
      Assert.AreEqual(occurrenceCount, chromiumEntry.Entries.Count(x => Path.GetFileName(x.Name) == fileName));
    }
  }
}
