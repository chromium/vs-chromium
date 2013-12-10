// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.Ipc;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumServer.Ipc.TypedEvents {
  [Export(typeof(ITypedEventSender))]
  public class TypedEventSender : ITypedEventSender {
    private readonly IIpcRequestIdFactory _requestIdFactory;
    private readonly IIpcResponseQueue _responseQueue;

    [ImportingConstructor]
    public TypedEventSender(IIpcResponseQueue responseQueue, IIpcRequestIdFactory requestIdFactory) {
      _responseQueue = responseQueue;
      _requestIdFactory = requestIdFactory;
    }

    public void SendEventAsync(TypedEvent typedEvent) {
      var ipcEvent = new IpcEvent {
        RequestId = _requestIdFactory.GetNextId(),
        Protocol = IpcProtocols.TypedMessage,
        Data = typedEvent
      };
      _responseQueue.Enqueue(ipcEvent);
    }
  }
}
