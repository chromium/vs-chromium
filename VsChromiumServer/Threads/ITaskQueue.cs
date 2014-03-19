// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.Threads {
  /// <summary>
  /// Ability to run tasks sequentially on a thread from the custom thread pool.
  /// </summary>
  public interface ITaskQueue {
    void Enqueue(string description, Action task);
  }
}
