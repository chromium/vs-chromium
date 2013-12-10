// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumCore.Ipc {
  /// <summary>
  /// An abstraction over reading/writing Ipc messages to an underlying transport
  /// mechanism.  Implementations are assumed to be safe to use from multiple threads,
  /// and the "ReadXxx" operations are blokcking until data is available.
  /// "WriteXxx" operations may be blocking if the Ipc message is big enough 
  /// that it requires buffering.
  /// </summary>
  public interface IIpcStream {
    void WriteRequest(IpcRequest request);
    void WriteResponse(IpcResponse response);
    IpcResponse ReadResponse();
    IpcRequest ReadRequest();
  }
}
