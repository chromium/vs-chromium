﻿// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc;

namespace VsChromium.Threads {
  class IpcRequestException : Exception {
    private readonly IpcRequest _request;

    public IpcRequestException(IpcRequest request, Exception inner)
      : base(string.Format("Error sending request {0} of type {1} to server", request.RequestId, request.Data.GetType().FullName), inner) {
      _request = request;
    }

    public IpcRequest Request { get { return _request; } }
  }
}