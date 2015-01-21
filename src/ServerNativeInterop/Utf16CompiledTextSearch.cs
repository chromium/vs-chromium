// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  public abstract class Utf16CompiledTextSearch : ICompiledTextSearch {
    private static readonly List<TextRange> NoResult = new List<TextRange>();

    public abstract int PatternLength { get; }

    public virtual void Dispose() {
    }

    public IEnumerable<TextRange> FindAll(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      return FindWorker(textFragment, progressTracker, int.MaxValue);
    }

    public TextRange? FindFirst(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      var result = FindWorker(textFragment, progressTracker, 1);
      if (result.Count == 0)
        return null;
      return result[0];
    }

    private List<TextRange> FindWorker(
      TextFragment textFragment,
      IOperationProgressTracker progressTracker,
      int maxResultSize) {
      List<TextRange> result = null;
      while (!textFragment.IsEmpty) {
        var searchHit = Search(textFragment);
        if (searchHit.IsNull)
          break;

        var range = new TextRange(searchHit.CharacterOffset, searchHit.CharacterCount);

        // Add to result collection
        if (result == null)
          result = new List<TextRange>();
        result.Add(range);

        textFragment = textFragment.Suffix(searchHit.FragmentEnd);

        maxResultSize--;
        progressTracker.AddResults(1);
        if (progressTracker.ShouldEndProcessing || maxResultSize <= 0)
          break;
      }

      return result ?? NoResult;
    }

    public abstract TextFragment Search(TextFragment textFragment);
  }
}