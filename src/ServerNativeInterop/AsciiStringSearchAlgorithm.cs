// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  public abstract class AsciiStringSearchAlgorithm : IDisposable {
    public abstract int PatternLength { get; }
    public abstract int SearchBufferSize { get; }

    public abstract void Search(ref NativeMethods.SearchParams searchParams);

    public virtual void Dispose() {
    }

    private static readonly List<FilePositionSpan> NoResult = new List<FilePositionSpan>();

    /// <summary>
    /// Find all occurrences of the pattern in the text block starting at
    /// |<paramref name="textPtr"/>| and containing |<paramref name="textLength"/>|
    /// characters.
    /// </summary>
    public IEnumerable<FilePositionSpan> SearchAll(
      IntPtr textPtr,
      int textLength,
      IOperationProgressTracker progressTracker) {
      return SearchAllWorker(textPtr, textLength, progressTracker);
    }

    private unsafe IEnumerable<FilePositionSpan> SearchAllWorker(
      IntPtr textPtr,
      int textLength,
      IOperationProgressTracker progressTracker) {
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
        if (progressTracker.ShouldEndProcessing)
          break;

        Search(ref searchParams);
        if (searchParams.MatchStart == IntPtr.Zero)
          break;

        if (result == null)
          result = new List<FilePositionSpan>();

        result.Add(new FilePositionSpan {
          // TODO(rpaquay): We are limited to 2GB for now.
          Position = Pointers.Offset32(textPtr, searchParams.MatchStart),
          Length = searchParams.MatchLength
        });
        progressTracker.AddResults(1);
      }
      return result ?? NoResult;
    }
  }
}
