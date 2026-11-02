// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc {
  public class IpcMessage {
    public long RequestId { get; set; }

    /// <summary>
    /// See <see cref="IpcProtocols"/>
    /// </summary>
    public string Protocol { get; set; }

    public IpcMessageData Data { get; set; }

    public override string ToString() {
      return Data?.ToString() ?? GetType().Name;
    }
  }
}
