// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  public abstract class CompiledTextSearchBase : ICompiledTextSearch {
    private static readonly List<TextRange> NoResult = new List<TextRange>();

    protected abstract int SearchBufferSize { get; }

    /// <summary>
    /// Perform a single search step given <paramref name="searchParams"/>.
    /// <see cref="NativeMethods.SearchParams.TextStart"/> and <see
    /// cref="NativeMethods.SearchParams.TextLength"/> define the range of the
    /// search between each call.
    /// On the first call, <see cref="NativeMethods.SearchParams.MatchStart"/>
    /// is <code>null</code>. On subsequent calls, <see
    /// cref="NativeMethods.SearchParams.MatchStart"/> is the result of the
    /// previous call.
    /// The implementation is responsible for setting the value of <see
    /// cref="NativeMethods.SearchParams.MatchStart"/>: Either the pointer to
    /// the search hit, or <code>null</code> if no match is found.
    /// </summary>
    /// <param name="searchParams"></param>
    protected abstract void Search(ref NativeMethods.SearchParams searchParams);

    /// <summary>
    /// If the search is abandonned before all hits have been found, this method
    /// is called to allow the implementation to cleanup intermediate data
    /// structures used by the implementation.
    /// </summary>
    protected abstract void CancelSearch(ref NativeMethods.SearchParams searchParams);

    public virtual void Dispose() {
    }

    public IList<TextRange> FindAll(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      if (progressTracker.ShouldEndProcessing)
        return NoResult;
      return FindWorker(textFragment, progressTracker, int.MaxValue);
    }

    public TextRange? FindFirst(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      var result = FindWorker(textFragment, progressTracker, 1);
      if (result.Count == 0)
        return null;
      return result.First();
    }

    private unsafe List<TextRange> FindWorker(
      TextFragment textFragment,
      IOperationProgressTracker progressTracker,
      int maxResultSize) {
      List<TextRange> result = null;

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
        var range = new TextRange(searchHit.CharacterOffset, searchHit.CharacterCount);

        // Add to result collection
        if (result == null)
          result = new List<TextRange>();
        result.Add(range);

        // Check it is time to end processing early.
        maxResultSize--;
        if (maxResultSize <= 0 || progressTracker.ShouldEndProcessing) {
          CancelSearch(ref searchParams);
          break;
        }
      }

      return result ?? NoResult;
    }
  }
}