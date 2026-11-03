// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestSearchCode : TestServerBase {
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
      const string searchPattern = "Test directory looking like";

      VerifySearchCodeResponse(_server, searchPattern, Options.MatchCase, _testFile.Directory, 1);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(_server, searchPatternLower, Options.None, _testFile.Directory, 1);
    }

    [TestMethod]
    public void InvalidRegexReturnsRecoverableError() {
      try {
        SendSearchCodeRequest(_server, "foo(", Options.Regex);
        Assert.Fail("The search should fail with an invalid regular expression.");
      }
      catch (ServerErrorException e) {
        Assert.AreEqual(ServerErrorKind.Recoverable, e.Error.Kind);
        Assert.IsFalse(string.IsNullOrEmpty(e.Error.Message));
      }
    }

    [Flags]
    private enum Options {
      None = 0,
      MatchCase = 0x01,
      Regex = 0x02,
    }

    private static SearchCodeResult SendSearchCodeRequest(IServerClient server, string searchPattern, Options options) {
      return SendRequest(server, client => client.SearchCodeAsync(new SearchParams {
        Query = new SearchQuery {
          Text = searchPattern,
          MatchCase = options.HasFlag(Options.MatchCase),
          Regex = options.HasFlag(Options.Regex),
        },
        MaxResults = 2000,
      }));
    }

    private static void VerifySearchCodeResponse(
        IServerClient server,
        string searchPattern,
        Options options,
        DirectoryInfo chromiumDirectory,
        int occurrenceCount,
        params int[] positionsAndLengths) {
      var response = SendSearchCodeRequest(server, searchPattern, options);
      Assert.IsNotNull(response, "Server did not respond within timeout.");
      Assert.IsNotNull(response.Projects);

      Assert.AreEqual(1, response.Projects.Count);
      var chromiumEntry = response.Projects[0];
      Assert.AreEqual(chromiumDirectory.FullName, chromiumEntry.RootPath);

      chromiumEntry.Files.ForAll((index, x) => {
        Debug.WriteLine(string.Format("File name: \"{0}\"", x.RelativePath));
        Assert.IsNotNull(x.Matches);
        Assert.IsTrue(x.Matches.Count > 0);
        x.Matches.ForEach(y => {
          Debug.WriteLine(string.Format("   Text position: offset={0}, length={1}, text={2}", y.Position, y.Length, ExtractFileText(chromiumEntry, x, y)));
          if (positionsAndLengths != null && positionsAndLengths.Length > 0) {
            Assert.AreEqual(positionsAndLengths[index * 2], y.Position);
            Assert.AreEqual(positionsAndLengths[(index * 2) + 1], y.Length);
          }
        });
      });
      Assert.AreEqual(occurrenceCount, chromiumEntry.Files.Count);
    }

    private static string ExtractFileText(ProjectMatches chromiumEntry, FileMatches fileEntry, TextSpan span) {
      var path = PathHelpers.CombinePaths(chromiumEntry.RootPath, fileEntry.RelativePath);
      if (!File.Exists(path))
        return string.Format("File not found: {0}", path);
      var text = File.ReadAllText(path);
      var offset = span.Position;
      var length = Math.Min(80, span.Length);
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
