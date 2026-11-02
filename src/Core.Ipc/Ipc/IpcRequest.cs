// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc {
  public class IpcRequest : IpcMessage {
    /// <summary>
    /// Specify if server should wait for this request to be fully processed before 
    /// starting processing the next one marked as <see cref="RunOnSequentialQueue"/> .
    /// By default, requests are executed in parallel (best effort) to ensure long running
    /// requests don't make the server unresponsive.
    /// </summary>
    public bool RunOnSequentialQueue { get; set; }
  }
}
