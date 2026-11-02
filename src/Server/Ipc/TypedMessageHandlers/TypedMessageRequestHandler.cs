// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  /// <summary>
  /// Base class of the handlers of the requests of type <typeparamref name="TRequest"/>.
  /// </summary>
  public abstract class TypedMessageRequestHandler<TRequest> : ITypedMessageRequestHandler
    where TRequest : TypedRequest {
    public bool CanProcess(TypedRequest request) {
      return request is TRequest;
    }

    public abstract TypedResponse Process(TypedRequest typedRequest);
  }
}
