// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc {
  public class ErrorResponse : IpcMessageData {
    public string Message { get; set; }

    public string FullTypeName { get; set; }

    public string StackTrace { get; set; }

    public ErrorResponse InnerError { get; set; }
  }
}
