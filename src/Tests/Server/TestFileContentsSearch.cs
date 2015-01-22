// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileContentsSearch : MefTestBase {
    private CompositionContainer _container;
    private ICompiledTextSearchDataFactory _factory;

    [TestInitialize]
    public void Initialize() {
      _container = SetupServerMefContainer();
      _factory = _container.GetExportedValue<ICompiledTextSearchDataFactory>();
    }

    [TestCleanup]
    public void Cleanup() {
      _container.Dispose();
    }

    [TestMethod]
    public void SingleOccurenceWorks() {
      const string text = "This is a piece of text";
      const string pattern = "piece";
      var result = PerformSearch(text, pattern);
      Assert.AreEqual(1, result.Count);
      Assert.AreEqual(10, result[0].CharacterOffset);
      Assert.AreEqual(5, result[0].CharacterCount);
    }

    [TestMethod]
    public void SingleOccurenceWithWildcardsWorks() {
      const string text = "This is a piece of text";
      const string pattern = "piece*text";
      var result = PerformSearch(text, pattern);
      Assert.AreEqual(1, result.Count);
      Assert.AreEqual(10, result[0].CharacterOffset);
      Assert.AreEqual(13, result[0].CharacterCount);
    }

    [TestMethod]
    public void SingleOccurenceWithWildcardsWorks2() {
      const string text = "Test directory looking like a local Chromium enlistment.";
      const string searchPattern = "Test*directory*looking*like";
      var result = PerformSearch(text, searchPattern);
      Assert.AreEqual(1, result.Count);
      Assert.AreEqual(0, result[0].CharacterOffset);
      Assert.AreEqual(27, result[0].CharacterCount);
    }

    [TestMethod]
    public void NoOccurenceWithWildcardsWorks() {
      const string text = "This is a piece of text";
      const string searchPattern = "piece*text2";
      var result = PerformSearch(text, searchPattern);
      Assert.AreEqual(0, result.Count);
    }

    private IList<TextRange> PerformSearch(string text, string searchPattern) {
      var result1 = PerformSearch(() => Utils.CreateAsciiFileContents(text), searchPattern);
      var result2 = PerformSearch(() => Utils.CreateUtf16FileContents(text), searchPattern);
      Assert.IsTrue(result1.SequenceEqual(result2));
      return result1;
    }

    private IList<TextRange> PerformSearch(
      Func<FileContents> fileContentsFactory,
      string searchPattern) {
      var searchParams = new SearchParams {
        SearchString = searchPattern,
        MatchCase = true,
        MaxResults = 1000,
        Regex = false,
        Re2 = false,
      };
      using (var searchData = _factory.Create(searchParams)) {
        var contents = fileContentsFactory();
        var result = contents.FindAll(searchData,
          contents.TextRange, OperationProgressTracker.None);
        return result;
      }
    }
  }
}
