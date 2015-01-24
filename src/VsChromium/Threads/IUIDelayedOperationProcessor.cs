// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Threads {
  /// <summary>
  /// Allows asynchronous execution of actions on the UIthread after a
  /// specified delay.
  /// </summary>
  public interface IUIDelayedOperationProcessor {
    void Post(DelayedOperation operation);
  }
}