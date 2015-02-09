// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.ServerProxy;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestSearchText : TestServerBase {
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
      const string searchPattern = "Test directory looking like";

      VerifySearchTextResponse(_server, searchPattern, Options.MatchCase, _testFile.Directory, 1);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchTextResponse(_server, searchPatternLower, Options.None, _testFile.Directory, 1);
    }

    [Flags]
    private enum Options {
      None = 0,
      MatchCase = 0x01,
      Regex = 0x02,
    }

    private static void VerifySearchTextResponse(
        ITypedRequestProcessProxy server,
        string searchPattern,
        Options options,
        DirectoryInfo chromiumDirectory,
        int occurrenceCount,
        params int[] positionsAndLengths) {
      var response = SendRequest<SearchTextResponse>(server, new SearchTextRequest {
        SearchParams = new SearchParams {
          SearchString = searchPattern,
          MaxResults = 2000,
          MatchCase = options.HasFlag(Options.MatchCase),
          Regex = options.HasFlag(Options.Regex),
        }
      }, ServerResponseTimeout)();
      Assert.IsNotNull(response, "Server did not respond within timeout.");
      Assert.IsNotNull(response.SearchResults);
      Assert.IsNotNull(response.SearchResults.Entries);

      Assert.AreEqual(1, response.SearchResults.Entries.Count);
      var chromiumEntry = response.SearchResults.Entries[0] as DirectoryEntry;
      Assert.IsNotNull(chromiumEntry);
      Assert.AreEqual(chromiumDirectory.FullName, chromiumEntry.Name);

      chromiumEntry.Entries.ForAll((index, x) => {
        Debug.WriteLine(string.Format("File name: \"{0}\"", x.Name));
        Assert.IsNotNull(x.Data);
        Assert.IsTrue(x.Data is FilePositionsData);
        ((FilePositionsData)x.Data).Positions.ForEach(y => {
          Debug.WriteLine(string.Format("   Text position: offset={0}, length={1}, text={2}", y.Position, y.Length, ExtractFileText(chromiumEntry, x, y)));
          if (positionsAndLengths != null && positionsAndLengths.Length > 0) {
            Assert.AreEqual(positionsAndLengths[index * 2], y.Position);
            Assert.AreEqual(positionsAndLengths[(index * 2) + 1], y.Length);
          }
        });
      });
      Assert.AreEqual(occurrenceCount, chromiumEntry.Entries.Count);
    }

    private static string ExtractFileText(DirectoryEntry chromiumEntry, FileSystemEntry fileSystemEntry, FilePositionSpan filePositionSpan) {
      var path = PathHelpers.CombinePaths(chromiumEntry.Name, fileSystemEntry.Name);
      if (!File.Exists(path))
        return string.Format("File not found: {0}", path);
      var text = File.ReadAllText(path);
      var offset = filePositionSpan.Position;
      var length = Math.Min(80, filePositionSpan.Length);
      if (offset < 0)
        return "<Invalid offset>";
      if (length < 0)
        return "<Invalid length>";
      if (offset + length > text.Length)
        return "<Invalid span>";
      var extract = text.Substring(offset, length);
      return extract;
    }
  }
}
