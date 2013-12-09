// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumPackage.Server {
  public interface ITypedRequestProcessProxy : IDisposable {
    void RunAsync(TypedRequest request, Action<TypedResponse> callback);
    event Action<TypedEvent> EventReceived;
  }
}
