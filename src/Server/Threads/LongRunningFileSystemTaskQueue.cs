// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Threading;

namespace VsChromium.Server.Threads {
  [Export(typeof(ILongRunningFileSystemTaskQueue))]
  public class LongRunningFileSystemTaskQueue : ILongRunningFileSystemTaskQueue {
    private readonly ITaskQueue _taskQueue;

    [ImportingConstructor]
    public LongRunningFileSystemTaskQueue(ITaskQueueFactory taskQueueFactory) {
      _taskQueue = taskQueueFactory.CreateQueue("Long Running FileSystem Task Queue");
    }

    public void Enqueue(TaskId id, Action<CancellationToken> task) {
      _taskQueue.Enqueue(id, task);
    }

    public void EnqueueUnique(Action<CancellationToken> task) {
      _taskQueue.EnqueueUnique(task);
    }

    public void CancelCurrentTask() {
      _taskQueue.CancelCurrentTask();
    }

    public void CancelAll() {
      _taskQueue.CancelAll();
    }
  }
}