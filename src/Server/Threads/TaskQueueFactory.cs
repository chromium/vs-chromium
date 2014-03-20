// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;

namespace VsChromium.Server.Threads {
  [Export(typeof(ITaskQueueFactory))]
  public class TaskQueueFactory : ITaskQueueFactory {
    private readonly ICustomThreadPool _customThreadPool;

    [ImportingConstructor]
    public TaskQueueFactory(ICustomThreadPool customThreadPool) {
      _customThreadPool = customThreadPool;
    }

    public ITaskQueue CreateQueue() {
      return new TaskQueue(_customThreadPool);
    }
  }
}
