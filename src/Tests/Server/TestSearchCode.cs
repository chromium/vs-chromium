// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Search;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestSearchCode : MefTestBase {
    private static CompositionContainer _container;
    private static FileInfo _testFile;
    private static IFileRegistrationTracker _registry;
    private static ISearchEngine _searchEngine;
    private static readonly EventWaitHandle _serverReadyEvent = new ManualResetEvent(false);

    [ClassInitialize]
    public static void Initialize(TestContext context) {
      _container = SetupServerMefContainer();
      _registry = _container.GetExportedValue<IFileRegistrationTracker>();
      _searchEngine = _container.GetExportedValue<ISearchEngine>();
      _searchEngine.FilesLoaded += (sender, result) => _serverReadyEvent.Set();
      _testFile = Utils.GetChromiumTestEnlistmentFile();
      _registry.RegisterFileAsync(new FullPath(_testFile.FullName));
    }

    [ClassCleanup]
    public static void Cleanup() {
      _container.Dispose();
    }

    [TestMethod]
    public void SingleOccurrenceWorks() {
      const string searchPattern = "Test directory looking like";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 1);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 1);
    }

    [TestMethod]
    public void MultipleOccurrenceWorks() {
      const string searchPattern = "Nothing here. Just making sure the directory exists.";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 3);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 3);
    }

    [TestMethod]
    public void SingleWildcardWorks() {
      const string searchPattern = "Test*looking";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 1, 0, 22);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 1, 0, 22);
    }

    [TestMethod]
    public void SingleWildcardWorks2() {
      const string searchPattern = "looking*like";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 1, 15, 12);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 1, 15, 12);
    }

    [TestMethod]
    public void MultipleWildcardsWorks() {
      const string searchPattern = "Test*looking*like";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 1, 0, 27);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 1, 0, 27);
    }

    [TestMethod]
    public void MultipleWildcardsWorks2() {
      const string searchPattern = "Test*directory*looking*like";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 1, 0, 27);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 1, 0, 27);
    }

    [TestMethod]
    public void MultipleWildcardsWorks3() {
      const string searchPattern = "directory*looking*like";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 1, 5, 22);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 1, 5, 22);
    }

    [TestMethod]
    public void EscapeWildcardIsIgnored() {
      const string searchPattern = @"foo\* bar";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 0);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 0);
    }

    [TestMethod]
    public void EscapeWildcardIsIgnored2() {
      const string searchPattern = @"foo\*\\bar";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase, 0);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None, 0);
    }

    [TestMethod]
    public void MultipleWholeWordWorks() {
      const string searchPattern = "directory";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase | Options.MatchWholeWord, 4);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None | Options.MatchWholeWord, 4);
    }

    [TestMethod]
    public void MultipleWholeWordWorks2() {
      const string searchPattern = "irectory";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase | Options.MatchWholeWord, 0);

      var searchPatternLower = searchPattern.ToLowerInvariant();
      VerifySearchCodeResponse(searchPatternLower, Options.None | Options.MatchWholeWord, 0);
    }

    [TestMethod]
    public void RegexWorks() {
      const string searchPattern = "Test directory looking like";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase | Options.Regex, 1);
      VerifySearchCodeResponse(searchPattern, Options.Regex, 1);
    }

    [TestMethod]
    public void RegexWorks2() {
      const string searchPattern = "[a-z]+";

      VerifySearchCodeResponse(searchPattern, Options.MatchCase | Options.Regex, 102);
      VerifySearchCodeResponse(searchPattern, Options.Regex, 104);
    }

    [Flags]
    private enum Options {
      None = 0,
      MatchCase = 0x01,
      Regex = 0x02,
      MatchWholeWord = 0x04,
    }

    private static void VerifySearchCodeResponse(
      string searchPattern,
      Options options,
      int occurrenceCount,
      params int[] positionsAndLengths) {
      if (!_serverReadyEvent.WaitOne(TimeSpan.FromSeconds(5.0))) {
        Assert.Fail("Search engine did not load files within timeout. This is due to an error or a (very) slow file system.");
      }
      var searchParams = new SearchParams {
        SearchString = searchPattern,
        MaxResults = 2000,
        MatchCase = options.HasFlag(Options.MatchCase),
        Regex = options.HasFlag(Options.Regex),
        IncludeSymLinks = false,
        MatchWholeWord = options.HasFlag(Options.MatchWholeWord),
        UseRe2Engine = true,
      };

      var searchResult = _searchEngine.SearchCode(searchParams);
      Assert.IsNotNull(searchResult);

      searchResult.Entries.ForAll((index, entry) => {
        Debug.WriteLine(string.Format("File name: \"{0}\"", entry.FileName));
        entry.Spans.ForAll(span => {
          Assert.IsNotNull(span);
          Debug.WriteLine(string.Format("   Text position: offset={0}, length={1}, text=\"{2}\"",
              span.Position,
              span.Length,
              ExtractFileText(entry.FileName.FullPath, span)));
          if (positionsAndLengths != null && positionsAndLengths.Length > 0) {
            Assert.AreEqual(positionsAndLengths[index * 2], span.Position);
            Assert.AreEqual(positionsAndLengths[(index * 2) + 1], span.Length);
          }
        });
      });
      var hitCount = searchResult.Entries.Aggregate(0, (c, entry) => c += entry.Spans.Count);
      Assert.AreEqual(occurrenceCount, hitCount);
    }

    private static string ExtractFileText(FullPath filepath, FilePositionSpan filePositionSpan) {
      var path = filepath.Value;
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
