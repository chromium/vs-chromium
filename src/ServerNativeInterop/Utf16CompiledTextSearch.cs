// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  public abstract class Utf16CompiledTextSearch : ICompiledTextSearch {
    public abstract int PatternLength { get; }

    public virtual void Dispose() {
    }

    public IEnumerable<FilePositionSpan> FindAll(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      while (!textFragment.IsEmpty) {
        var searchHit = Search(textFragment);
        if (searchHit.IsNull)
          break;

        progressTracker.AddResults(1);
        if (progressTracker.ShouldEndProcessing)
          yield break;

        yield return new FilePositionSpan {
          // TODO(rpaquay): We are limited to 2GB for now.
          Position = (int)searchHit.CharacterOffset, 
          Length = (int)searchHit.CharacterCount
        };
        textFragment = textFragment.Suffix(searchHit.FragmentEnd);
      }
    }

    public FilePositionSpan? FindFirst(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      var result = FindAll(textFragment, progressTracker).ToList();
      if (result.Count == 0)
        return null;
      return result[0];
    }

    public abstract TextFragment Search(TextFragment textFragment);
  }
}