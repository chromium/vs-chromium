// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class NullCompiledTextSearch : ICompiledTextSearch {
    private static readonly IList<TextRange> NoResult = new List<TextRange>().ToReadOnlyCollection();

    public static NullCompiledTextSearch Instance = new NullCompiledTextSearch();

    public void Dispose() {
    }

    public IList<TextRange> FindAll(
      TextFragment textFragment,
      Func<TextRange, TextRange?> postProcess,
      IOperationProgressTracker progressTracker) {
      return NoResult;
    }

    public TextRange? FindFirst(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      return null;
    }
  }
}