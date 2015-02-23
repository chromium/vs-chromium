// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.ProtoBuf;
using VsChromium.Core.Ipc.TypedMessages;
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
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          // Send "AddFile" request, and wait for response.
          var response1 = SendRequest<GetFileSystemResponse>(server, new GetFileSystemRequest {
            KnownVersion = -1
          }, ServerResponseTimeout)();

          // Send "AddFile" request, and wait for response.
          SendRegisterFileRequest(server, testFile, ServerResponseTimeout);

          while (true) {
            var response = SendRequest<GetFileSystemResponse>(server, new GetFileSystemRequest {
              KnownVersion = -1
            }, ServerResponseTimeout)();
            if (response != null && response.Tree.Version != response1.Tree.Version) {
              //DisplayTreeStats(container.GetExport<IProtoBufSerializer>().Value, response, false);
              TestSearch(server);
              break;
            }
            Trace.WriteLine("Tree version has not changed yet.");
            Thread.Sleep(500);
          }
        }
      }
    }

    private static void TestSearch(ITypedRequestProcessProxy server) {
      while (true) {
        var response = SendRequest<SearchFileNamesResponse>(server, new SearchFileNamesRequest {
          SearchParams = {
            SearchString = "histogram"
          }
        }, ServerResponseTimeout)();
        if (response != null && response.SearchResult != null && response.SearchResult.Entries.Count > 0)
          break;
        Trace.WriteLine("It looks like the file indexer has not yet finished computing the new state.");
        Thread.Sleep(500);
      }

      while (true) {
        var response = SendRequest<SearchCodeResponse>(server, new SearchCodeRequest {
          SearchParams = {
            SearchString = "histogram"
          }
        }, ServerResponseTimeout)();
        if (response != null && response.SearchResults != null && response.SearchResults.Entries.Count > 0) {
          Trace.WriteLine(string.Format("Found {0} files matching search text.", response.SearchResults.Entries.Count));
          break;
        }
        Trace.WriteLine("It looks like the file indexer has not yet finished computing the new state.");
        Thread.Sleep(500);
      }
      var response4 = SendRequest<SearchCodeResponse>(server, new SearchCodeRequest {
        SearchParams = {
          SearchString = "histogram"
        }
      }, TimeSpan.FromSeconds(0.01))();
      var response5 = SendRequest<SearchCodeResponse>(server, new SearchCodeRequest {
        SearchParams = {
          SearchString = "histogram"
        }
      }, TimeSpan.FromSeconds(0.01))();
      var response6 = SendRequest<SearchCodeResponse>(server, new SearchCodeRequest {
        SearchParams = {
          SearchString = "histogram"
        }
      }, TimeSpan.FromSeconds(0.01))();
      var response7 = SendRequest<SearchCodeResponse>(server, new SearchCodeRequest {
        SearchParams = {
          SearchString = "histogram"
        }
      }, TimeSpan.FromSeconds(0.01))();
      var response8 = SendRequest<SearchCodeResponse>(server, new SearchCodeRequest {
        SearchParams = {
          SearchString = "histogram"
        }
      }, TimeSpan.FromSeconds(0.01))();
      var response9 = SendRequest<SearchCodeResponse>(server, new SearchCodeRequest {
        SearchParams = {
          SearchString = "histogram"
        }
      }, TimeSpan.FromSeconds(10.0))();
    }

    private void DisplayTreeStats(IProtoBufSerializer serializer, GetFileSystemResponse response, bool verbose) {
      Trace.WriteLine("=====================================================================");
      Trace.WriteLine("FileSystem tree stats:");
      {
        var mem = new MemoryStream();
        var sw = new Stopwatch();
        var ipcResponse = new IpcResponse {
          RequestId = 0,
          Protocol = IpcProtocols.TypedMessage,
          Data = response
        };
        sw.Start();
        serializer.Serialize(mem, ipcResponse);
        sw.Stop();
        Trace.WriteLine(string.Format("ProtoBuf request of {0:n0} bytes serialized in {1} msec.", mem.Length,
                                      sw.ElapsedMilliseconds));
      }

      var stats = new TreeStats();
      stats.ProcessTree(null, "", response.Tree.Root);
      Trace.WriteLine(string.Format("Directory count: {0:n0}", stats.DirectoryCount));
      Trace.WriteLine(string.Format("File count: {0:n0}", stats.FileCount));
      Trace.WriteLine(string.Format("Total File size: {0:n0} bytes", stats.TotalSize));
      if (verbose) {
        Trace.WriteLine("=====================================================================");
        Trace.WriteLine(" Files sorted by count");
        foreach (var item in stats.Extensions.OrderByDescending(x => x.Value.FileCount)) {
          Trace.WriteLine(string.Format("Extension \"{0}\": {1:n0} files, {2:n0} bytes", item.Key.ToUpperInvariant(),
                                        item.Value.FileCount, item.Value.TotalSize));
        }

        Trace.WriteLine("=====================================================================");
        Trace.WriteLine(" Files sorted by total length");
        foreach (var item in stats.Extensions.OrderByDescending(x => x.Value.TotalSize)) {
          Trace.WriteLine(string.Format("Extension \"{0}\": {2:n0} bytes, {1:n0} files", item.Key.ToUpperInvariant(),
                                        item.Value.FileCount, item.Value.TotalSize));
        }

        OutputDirectorytree(0, "", stats.RootDirectory);
      }
    }

    private void OutputDirectorytree(int indent, string parentPath, TreeStats.DirectoryItem entry) {
      if (entry.Name != null) {
        var text = "";
        for (var i = 0; i < indent; i++) {
          text += "| ";
        }
        text += entry.Name;
        Trace.WriteLine(text);
      }

      foreach (var x in entry.Children) {
        OutputDirectorytree(indent + 1, parentPath + entry.Name, x);
      }
    }

    private class TreeStats {
      public TreeStats() {
        Extensions = new Dictionary<string, FileExtensionStats>();
      }

      public int DirectoryCount { get; set; }
      public int FileCount { get; set; }
      public long TotalSize { get; set; }
      public Dictionary<string, FileExtensionStats> Extensions { get; set; }
      public DirectoryItem RootDirectory { get; set; }

      public void ProcessTree(DirectoryItem parent, string parentPath, FileSystemEntry entry) {
        if (entry is FileEntry)
          ProcessFile(parentPath, (FileEntry)entry);
        else
          ProcessDirectory(parent, parentPath, (DirectoryEntry)entry);
      }

      private void ProcessDirectory(DirectoryItem parent, string parentPath, DirectoryEntry entry) {
        DirectoryCount++;
        DirectoryItem newParent;
        string newPath;
        if (parent == null) {
          RootDirectory = new TreeStats.DirectoryItem();
          newParent = RootDirectory;
          newPath = parentPath;
        } else {
          var newDir = new DirectoryItem {
            Name = entry.Name
          };
          parent.Children.Add(newDir);
          newParent = newDir;
          newPath = Path.Combine(parentPath, entry.Name);
        }
        foreach (var x in entry.Entries) {
          ProcessTree(newParent, newPath, x);
        }
      }

      private void ProcessFile(string parentPath, FileEntry entry) {
        if (entry.Name == null)
          return;

        Assert.AreNotEqual("", entry.Name);

        var fullName = Path.Combine(parentPath, entry.Name);
        var fileInfo = new FileInfo(fullName);
        if (!fileInfo.Exists)
          return;

        var fileLength = fileInfo.Length;
        {
          FileCount++;
          TotalSize += fileLength;
        }

        {
          var ext = Path.GetExtension(entry.Name);
          if (!Extensions.ContainsKey(ext)) {
            Extensions.Add(ext, new FileExtensionStats {
              Extension = ext
            });
          }
          var stats = Extensions[ext];
          stats.FileCount++;
          stats.TotalSize += fileLength;
          stats.Files.Add(fileInfo.FullName);
        }

        return;
      }

      public class DirectoryItem {
        public DirectoryItem() {
          Children = new List<DirectoryItem>();
        }

        public string Name { get; set; }
        public List<DirectoryItem> Children { get; set; }
      }

      public class FileExtensionStats {
        public FileExtensionStats() {
          Files = new List<string>();
        }

        public string Extension { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
        public List<string> Files { get; set; }
      }
    }
  }
}
