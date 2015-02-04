// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Utility;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Tests.NativeInterop {
  [TestClass]
  public class TestAsciiSearch {
    [TestMethod]
    public void HeapAllocWorks() {
      var block = HeapAllocStatic.Alloc(1024);
      block.Close();
    }

    [TestMethod]
    public void AsciiSearchForVariousPatternsWorks() {
      const long oneKB = 1024L;
      const long oneMB = 1024 * oneKB;
      const long tenMB = 10 * oneMB;

#if FULL_THROUGHPUT_TEST
      const int iterationCount = 5;
      const long one100MB = 100 * oneMB;
#else
      const int iterationCount = 2;
      const long one100MB = 1 * oneMB;
#endif

      const int averageMatchCount = 100;
      const int noMatchCount = 0;

      const string shortPattern = "foo";
      const string longPattern = "foobarblahbleahabcdefghfgtrews"; // Can't be longer than 32!

      AsciiSearchForVariousPatternsWorks(4 * oneKB, shortPattern, NativeMethods.SearchOptions.kNone, averageMatchCount, iterationCount);
      AsciiSearchForVariousPatternsWorks(4 * oneKB, shortPattern, NativeMethods.SearchOptions.kNone, noMatchCount, iterationCount);

      AsciiSearchForVariousPatternsWorks(tenMB, shortPattern, NativeMethods.SearchOptions.kNone, averageMatchCount, iterationCount);
      AsciiSearchForVariousPatternsWorks(tenMB, shortPattern, NativeMethods.SearchOptions.kNone, noMatchCount, iterationCount);

      AsciiSearchForVariousPatternsWorks(one100MB, shortPattern, NativeMethods.SearchOptions.kNone, averageMatchCount, iterationCount);
      AsciiSearchForVariousPatternsWorks(one100MB, shortPattern, NativeMethods.SearchOptions.kNone, noMatchCount, iterationCount);

      AsciiSearchForVariousPatternsWorks(4 * oneKB, longPattern, NativeMethods.SearchOptions.kNone, averageMatchCount, iterationCount);
      AsciiSearchForVariousPatternsWorks(4 * oneKB, longPattern, NativeMethods.SearchOptions.kNone, noMatchCount, iterationCount);

      AsciiSearchForVariousPatternsWorks(tenMB, longPattern, NativeMethods.SearchOptions.kNone, averageMatchCount, iterationCount);
      AsciiSearchForVariousPatternsWorks(tenMB, longPattern, NativeMethods.SearchOptions.kNone, noMatchCount, iterationCount);

      AsciiSearchForVariousPatternsWorks(one100MB, longPattern, NativeMethods.SearchOptions.kNone, averageMatchCount, iterationCount);
      AsciiSearchForVariousPatternsWorks(one100MB, longPattern, NativeMethods.SearchOptions.kNone, noMatchCount, iterationCount);
    }

    public void AsciiSearchForVariousPatternsWorks(
      long blockByteLength,
      string pattern,
      NativeMethods.SearchOptions searchOptions,
      int patternOccurrenceCount,
      int iterationCount) {
      Trace.WriteLine(
        string.Format(
          "Searching {0} time(s) for pattern \"{1}\" with {2} occurrence(s) in a memory block of {3:n0} bytes.",
          iterationCount, pattern, patternOccurrenceCount, blockByteLength));

      Assert.IsTrue(iterationCount >= 1);
      using (var textBlock = HeapAllocStatic.Alloc(blockByteLength)) {
        FillWithNonNulCharacters(textBlock);

        SetSearchMatches(textBlock, pattern, patternOccurrenceCount);

        using (var search = new AsciiCompiledTextSearchStrStr(pattern, searchOptions)) {
          var sw = Stopwatch.StartNew();
          var matchCount = PerformSearch(textBlock, search, iterationCount);
          sw.Stop();
          Assert.AreEqual(patternOccurrenceCount, matchCount);
          Trace.WriteLine(string.Format("  StrStr: Found {0:n0} occurrence(s) {1} times in {2}s ({3:n0} KB/s.)",
                                        matchCount, iterationCount, sw.Elapsed.TotalSeconds,
                                        ComputeThroughput(sw, blockByteLength, iterationCount)));
        }
        using (var search = new AsciiCompiledTextSearchBoyerMoore(pattern, searchOptions)) {
          var sw = Stopwatch.StartNew();
          var matchCount = PerformSearch(textBlock, search, iterationCount);
          sw.Stop();
          Assert.AreEqual(patternOccurrenceCount, matchCount);
          Trace.WriteLine(string.Format("  Boyer-Moore: Found {0:n0} occurrence(s) {1} times in {2} s ({3:n0} KB/s.)",
                                        matchCount, iterationCount, sw.Elapsed.TotalSeconds,
                                        ComputeThroughput(sw, blockByteLength, iterationCount)));
        }
        using (var search = new AsciiCompiledTextSearchBndm32(pattern, searchOptions)) {
          var sw = Stopwatch.StartNew();
          var matchCount = PerformSearch(textBlock, search, iterationCount);
          sw.Stop();
          Assert.AreEqual(patternOccurrenceCount, matchCount);
          Trace.WriteLine(string.Format("  BNDM-32: Found {0:n0} occurrence(s) {1} times in {2} s ({3:n0} KB/s.)",
                                        matchCount, iterationCount, sw.Elapsed.TotalSeconds,
                                        ComputeThroughput(sw, blockByteLength, iterationCount)));
        }
        using (var search = new AsciiCompiledTextSearchBndm64(pattern, searchOptions)) {
          var sw = Stopwatch.StartNew();
          var matchCount = PerformSearch(textBlock, search, iterationCount);
          sw.Stop();
          Assert.AreEqual(patternOccurrenceCount, matchCount);
          Trace.WriteLine(string.Format("  BNDM-64: Found {0:n0} occurrence(s) {1} times in {2} s ({3:n0} KB/s.)",
                                        matchCount, iterationCount, sw.Elapsed.TotalSeconds,
                                        ComputeThroughput(sw, blockByteLength, iterationCount)));
        }
      }
    }

    private double ComputeThroughput(Stopwatch sw, long size, int repeat) {
      var kbytes = (size * repeat) / 1024L;
      var s = sw.Elapsed.TotalSeconds;
      return (double)kbytes / s;
    }

    private static int PerformSearch(
        SafeHeapBlockHandle textBlock,
        ICompiledTextSearch algo,
        int repeat) {
      int matchCount = 0;
      for (var i = 0; i < repeat; i++) {
        matchCount = algo.FindAll(
          new TextFragment(textBlock.Pointer, 0, (int) textBlock.ByteLength, sizeof (byte)),
          OperationProgressTracker.None).Count();
      }
      return matchCount;
    }

    private static unsafe void SetSearchMatches(SafeHeapBlockHandle block, string search, int matchCount) {
      if (matchCount <= 0)
        return;

      var p = (byte*)block.Pointer.ToPointer();
      var delta = (block.ByteLength - search.Length) / matchCount;
      for (var i = 0; i < matchCount; i++) {
        var offset = (long)i * delta;
        for (var j = 0; j < search.Length; j++) {
          p[offset + j] = (byte)search[j];
        }
      }
    }

    private static unsafe void FillWithNonNulCharacters(SafeHeapBlockHandle block) {
      var p = (byte*)block.Pointer.ToPointer();
      for (var i = 0L; i < block.ByteLength; i++) {
        p[i] = 0x01;
      }
    }
  }
}
