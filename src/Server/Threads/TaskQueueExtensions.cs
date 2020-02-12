// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using VsChromium.Core.Logging;

namespace VsChromium.Server.Threads {
  public static class TaskQueueExtensions {
    public static void ExecuteAsync(this ITaskQueue queue, Action<CancellationToken> task) {
      queue.Enqueue(new TaskId("Unique"), task);
    }
  }
}