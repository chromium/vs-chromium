// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;

namespace VsChromium.Server.Threads {
  /// <summary>
  /// We use a custom thread pool because 1) we want a reasonable amount of thread available for some 
  /// tasks and 2) we use System.Threading.Task extensively, which tends to make the .NET thread pool
  /// unavailable for periods of time.
  /// </summary>
  public interface ICustomThreadPool {
    void RunAsync(Action task);

    void RunAsync(Action task, TimeSpan delay);
  }
}
