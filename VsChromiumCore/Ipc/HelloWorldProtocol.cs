// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumCore.Ipc {
  public static class HelloWorldProtocol {
    public static IpcRequest Request {
      get {
        return new IpcRequest {
          RequestId = 1,
          Protocol = IpcProtocols.Hello,
          Data = new IpcStringData {
            Text = "Hi There!"
          }
        };
      }
    }

    public static IpcResponse Response {
      get {
        return new IpcResponse {
          RequestId = 1,
          Protocol = IpcProtocols.Hello,
          Data = new IpcStringData {
            Text = "Hello!"
          }
        };
      }
    }
  }
}
