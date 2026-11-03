// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestServerPerf : TestServerBase {
    [TestMethod]
    public void Dummy() {
    }

    //[TestMethod]
    public void TestServer() {
      var testFile = Utils.GetChromiumTestEnlistmentFile();

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<IServerClient>().Value) {
          var tree1 = SendGetFileSystemTreeRequest(server);

          // Send "files/register" request, and wait for response.
          SendRegisterFileRequest(server, testFile);

          while (true) {
            var tree = SendGetFileSystemTreeRequest(server);
            if (tree.Version != tree1.Version) {
              TestSearch(server);
              break;
            }
            Trace.WriteLine("Tree version has not changed yet.");
            Thread.Sleep(500);
          }
        }
      }
    }

    private static void TestSearch(IServerClient server) {
      var parameters = new SearchParams {
        Query = new SearchQuery {
          Text = "histogram"
        },
        MaxResults = 2000,
      };

      while (true) {
        var response = SendRequest(server, client => client.SearchPathsAsync(parameters));
        if (response != null && response.Projects.Count > 0)
          break;
        Trace.WriteLine("It looks like the file indexer has not yet finished computing the new state.");
        Thread.Sleep(500);
      }

      while (true) {
        var response = SendRequest(server, client => client.SearchCodeAsync(parameters));
        if (response != null && response.Projects.Count > 0) {
          Trace.WriteLine(string.Format("Found {0} files matching search text.", response.Projects[0].Files.Count));
          break;
        }
        Trace.WriteLine("It looks like the file indexer has not yet finished computing the new state.");
        Thread.Sleep(500);
      }

      for (var i = 0; i < 6; i++) {
        var sw = Stopwatch.StartNew();
        SendRequest(server, client => client.SearchCodeAsync(parameters));
        Trace.WriteLine(string.Format("Search took {0:n0} msec.", sw.ElapsedMilliseconds));
      }
    }
  }
}
