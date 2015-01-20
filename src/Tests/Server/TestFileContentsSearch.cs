// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.Search;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileContentsSearch : MefBaseTest {
    [TestMethod]
    public void SingleOccurenceWorks() {
      using (var container = SetupMefContainer()) {
        var factory = container.GetExportedValue<ICompiledTextSearchDataFactory>();

        var contents = CreateAsciiFileContents("This is a piece of text");
        var searchParams = new SearchParams {
          SearchString = "piece",
          MatchCase = true,
          MaxResults = 1000,
          Regex = false,
          Re2 = false,
        };
        var searchData = factory.Create(searchParams);
        var result = contents.Search(contents.TextRange, searchData, OperationProgressTracker.None);
        Assert.AreEqual(1, result.Count);
      }
    }


    [TestMethod]
    public void SingleOccurenceWithWildcardsWorks() {
      using (var container = SetupMefContainer()) {
        var factory = container.GetExportedValue<ICompiledTextSearchDataFactory>();

        var contents = CreateAsciiFileContents("This is a piece of text");
        var searchParams = new SearchParams {
          SearchString = "piece*text",
          MatchCase = true,
          MaxResults = 1000,
          Regex = false,
          Re2 = false,
        };
        var searchData = factory.Create(searchParams);
        var result = contents.Search(contents.TextRange, searchData, OperationProgressTracker.None);
        Assert.AreEqual(1, result.Count);
      }
    }

    [TestMethod]
    public void SingleOccurenceWithWildcardsWorks2() {
      const string searchPattern = "Test*directory*looking*like";

      using (var container = SetupMefContainer()) {
        var factory = container.GetExportedValue<ICompiledTextSearchDataFactory>();

        var contents = CreateAsciiFileContents("Test directory looking like a local Chromium enlistment.");
        var searchParams = new SearchParams {
          SearchString = searchPattern,
          MatchCase = true,
          MaxResults = 1000,
          Regex = false,
          Re2 = false,
        };
        var searchData = factory.Create(searchParams);
        var result = contents.Search(contents.TextRange, searchData, OperationProgressTracker.None);
        Assert.AreEqual(1, result.Count);
      }
    }

    [TestMethod]
    public void NoOccurenceWithWildcardsWorks() {
      using (var container = SetupMefContainer()) {
        var factory = container.GetExportedValue<ICompiledTextSearchDataFactory>();

        var contents = CreateAsciiFileContents("This is a piece of text");
        var searchParams = new SearchParams {
          SearchString = "piece*text2",
          MatchCase = true,
          MaxResults = 1000,
          Regex = false,
          Re2 = false,
        };
        var searchData = factory.Create(searchParams);
        var result = contents.Search(contents.TextRange, searchData, OperationProgressTracker.None);
        Assert.AreEqual(0, result.Count);
      }
    }
    private AsciiFileContents CreateAsciiFileContents(string text) {
      var memory = CreateAsciiMemory(text);
      var contents = new AsciiFileContents(memory, DateTime.Now);
      return contents;
    }

    private FileContentsMemory CreateAsciiMemory(string text) {
      var size = text.Length + 1;
      var handle = new SafeHGlobalHandle(Marshal.StringToHGlobalAnsi(text));
      return new FileContentsMemory(handle, size, 0, size);
    }
  }
}
