// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  /// <summary>
  /// Abstraction over text search algorithm pre-compiled with the search pattern.
  /// </summary>
  public interface ICompiledTextSearch : IDisposable {
    /// <summary>
    /// Find all occurrences of the search pattern in the given text fragment.
    /// </summary>
    IEnumerable<FilePositionSpan> SearchAll(
      TextFragment textFragment,
      IOperationProgressTracker progressTracker);

    /// <summary>
    /// Find the first occurrence of the search pattern in the given text fragment.
    /// </summary>
    FilePositionSpan? SearchOne(
      TextFragment textFragment,
      IOperationProgressTracker progressTracker);
  }
}