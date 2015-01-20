// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  public abstract class AsciiCompiledTextSearch : ICompiledTextSearch {

    public abstract int PatternLength { get; }
    public abstract int SearchBufferSize { get; }

    public abstract void Search(ref NativeMethods.SearchParams searchParams);
    public abstract void CancelSearch(ref NativeMethods.SearchParams searchParams);

    public virtual void Dispose() {
    }

    private static readonly List<FilePositionSpan> NoResult = new List<FilePositionSpan>();

    public IEnumerable<FilePositionSpan> SearchAll(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      if (progressTracker.ShouldEndProcessing)
        return NoResult;
      return SearchAllWorker(textFragment, progressTracker, int.MaxValue);
    }

    public FilePositionSpan? SearchOne(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      var result = SearchAllWorker(textFragment, progressTracker, 1);
      if (result.Count == 0)
        return null;
      return result.First();
    }

    private unsafe List<FilePositionSpan> SearchAllWorker(TextFragment textFragment, IOperationProgressTracker progressTracker, int maxResultSize) {
      List<FilePositionSpan> result = null;

      // Note: From C# spec: If E is zero, then no allocation is made, and
      // the pointer returned is implementation-defined. 
      byte* searchBuffer = stackalloc byte[this.SearchBufferSize];
      var searchParams = new NativeMethods.SearchParams {
        TextStart = textFragment.FragmentStart,
        // TODO(rpaquay): We are limited to 2GB for now.
        TextLength = (int)textFragment.CharacterCount,
        SearchBuffer = new IntPtr(searchBuffer),
      };

      while (true) {
        // Perform next search
        Search(ref searchParams);
        if (searchParams.MatchStart == IntPtr.Zero)
          break;

        var searchHit = textFragment.Sub(searchParams.MatchStart, searchParams.MatchLength);
        var filePositionSpan = new FilePositionSpan {
          // TODO(rpaquay): We are limited to 2GB for now.
          Position = (int)searchHit.CharacterOffset,
          Length = (int)searchHit.CharacterCount,
        };

        // Add to result collection
        if (result == null)
          result = new List<FilePositionSpan>();
        result.Add(filePositionSpan);

        // Check it is time to end processing early.
        maxResultSize--;
        progressTracker.AddResults(1);
        if (progressTracker.ShouldEndProcessing || maxResultSize <= 0) {
          CancelSearch(ref searchParams);
          break;
        }
      }

      return result ?? NoResult;
    }
  }
}
