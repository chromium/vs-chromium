// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Threads {
  /// <summary>
  /// Post and delay requests from Dispatch thread to the server.
  /// </summary>
  public interface IDispatchThreadServerRequestExecutor {
    void Post(DispatchThreadServerRequest request);

    event EventHandler ProcessStarted;
    event EventHandler<ErrorEventArgs> ProcessFatalError;

    bool IsServerRunning { get; }
  }
}
