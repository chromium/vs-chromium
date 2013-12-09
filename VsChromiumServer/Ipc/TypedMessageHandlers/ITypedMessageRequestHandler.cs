// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumServer.Ipc.TypedMessageHandlers {
  public interface ITypedMessageRequestHandler {
    bool CanProcess(TypedRequest request);
    TypedResponse Process(TypedRequest typedRequest);
  }
}
