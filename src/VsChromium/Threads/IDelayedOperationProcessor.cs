// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Threads {
  /// <summary>
  /// Allows asynchronous execution of actions on a background thread after a
  /// specified delay.
  /// </summary>
  public interface IDelayedOperationProcessor {
    /// <summary>
    /// Post an <paramref name="operation"/> to the queue for execution after
    /// the delay specified in the operation. If an operation with the same Id
    /// already exists in the queue, it is removed from the queue and replaced
    /// with the one passed in. The action callback will be invoked on a
    /// background thread, different from the caller thread. The implementation
    /// is guaranteed to be thread safe.
    /// </summary>
    void Post(DelayedOperation operation);
  }
}