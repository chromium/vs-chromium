// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  public abstract class UTF16StringSearchAlgorithm : IDisposable {
    public abstract int PatternLength { get; }

    public virtual void Dispose() {
    }

    public IEnumerable<FilePositionSpan> SearchAll(
      IntPtr text,
      int textLen,
      IOperationProgressTracker progressTracker) {

      var currentPtr = text;
      var remainingLength = textLen;
      while (true) {
        currentPtr = Search(currentPtr, remainingLength);
        if (currentPtr == IntPtr.Zero)
          break;

        progressTracker.AddResults(1);
        if (progressTracker.ShouldEndProcessing)
          yield break;

        yield return new FilePositionSpan {
          // TODO(rpaquay): We are limited to 2GB for now.
          Position = Pointers.Offset32(text, currentPtr), 
          Length = PatternLength
        };
        currentPtr += PatternLength * sizeof(char);
        remainingLength = textLen - Pointers.Offset32(text, currentPtr) - (PatternLength * sizeof(char));
      }
    }

    public abstract IntPtr Search(IntPtr text, int textLen);
  }
}