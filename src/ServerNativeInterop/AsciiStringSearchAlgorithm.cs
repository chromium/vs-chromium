// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  public abstract class AsciiStringSearchAlgorithm : IDisposable {
    public abstract int PatternLength { get; }
    public abstract int SearchBufferSize { get; }

    public abstract void Search(ref NativeMethods.SearchParams searchParams);
    public abstract void CancelSearch(ref NativeMethods.SearchParams searchParams);

    public virtual void Dispose() {
    }

    private static readonly List<FilePositionSpan> NoResult = new List<FilePositionSpan>();

    /// <summary>
    /// Find all occurrences of the pattern in the text block starting at
    /// |<paramref name="textPtr"/>| and containing |<paramref name="textLength"/>|
    /// characters.
    /// </summary>
    public IEnumerable<FilePositionSpan> SearchAll(
        string path,
        IntPtr textPtr,
        int textLength,
        IOperationProgressTracker progressTracker) {
      Stopwatch sw = null;
      if (textLength >= 3*1024*1024) {
        sw = Stopwatch.StartNew();
      }
      var result = SearchAllWorker(textPtr, textLength, progressTracker);
      if (textLength >= 3*1024*1024) {
        sw.Stop();
        Logger.Log("Search time took {0:n0} msec in file \"{1}\" of size {2:n0} bytes", sw.ElapsedMilliseconds, path, textLength);
      }
      return result;
    }

    private unsafe IEnumerable<FilePositionSpan> SearchAllWorker(
      IntPtr textPtr,
      int textLength,
      IOperationProgressTracker progressTracker) {
      if (progressTracker.ShouldEndProcessing) {
        return NoResult;
      }

      List<FilePositionSpan> result = null;
      // Note: From C# spec: If E is zero, then no allocation is made, and
      // the pointer returned is implementation-defined. 
      byte* searchBuffer = stackalloc byte[this.SearchBufferSize];
      var searchParams = new NativeMethods.SearchParams {
        TextStart = textPtr,
        TextLength = textLength,
        SearchBuffer = new IntPtr(searchBuffer),
      };
      while (true) {
        // Perform next search
        Search(ref searchParams);
        if (searchParams.MatchStart == IntPtr.Zero)
          break;

        // Add result
        if (result == null)
          result = new List<FilePositionSpan>();
        result.Add(new FilePositionSpan {
          // TODO(rpaquay): We are limited to 2GB for now.
          Position = Pointers.Offset32(textPtr, searchParams.MatchStart),
          Length = searchParams.MatchLength
        });

        // Check it is time to end processing early.
        progressTracker.AddResults(1);
        if (progressTracker.ShouldEndProcessing) {
          CancelSearch(ref searchParams);
          break;
        }
      }
      return result ?? NoResult;
    }
  }
}
