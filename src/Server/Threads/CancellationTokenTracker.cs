// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Threading;

namespace VsChromium.Server.Threads {
  /// <summary>
  /// Keeps track of a single cancellation token (the last one created) in a thread safe manner.
  /// </summary>
  public class CancellationTokenTracker {
    private CancellationTokenSource _currentTokenSource;

    /// <summary>
    /// Create a new token, replacing the previous one (which is now orphan)
    /// </summary>
    public CancellationToken NewToken() {
      var newSource = new CancellationTokenSource();
      Interlocked.Exchange(ref _currentTokenSource, newSource);
      return newSource.Token;
    }

    /// <summary>
    /// Cancel the last token created (if there was one)
    /// </summary>
    public void CancelCurrent() {
      var currentSource = Interlocked.Exchange(ref _currentTokenSource, null);
      if (currentSource != null) {
        currentSource.Cancel();
      }
    }
  }
}