// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Threads;

namespace VsChromium.Server.Threads {
  public class TaskQueueFactory : ITaskQueueFactory {
    private readonly ICustomThreadPool _customThreadPool;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TaskQueueFactory(ICustomThreadPool customThreadPool, IDateTimeProvider dateTimeProvider) {
      _customThreadPool = customThreadPool;
      _dateTimeProvider = dateTimeProvider;
    }

    public ITaskQueue CreateQueue(string description) {
      return new TaskQueue(description, _customThreadPool, _dateTimeProvider);
    }
  }
}
