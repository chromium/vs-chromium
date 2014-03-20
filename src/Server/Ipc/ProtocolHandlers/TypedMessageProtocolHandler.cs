// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.Ipc.TypedMessageHandlers;

namespace VsChromium.Server.Ipc.ProtocolHandlers {
  [Export(typeof(IProtocolHandler))]
  public class TypedMessageProtocolHandler : ProtocolHandler {
    private readonly IEnumerable<ITypedMessageRequestHandler> _handlers;

    [ImportingConstructor]
    public TypedMessageProtocolHandler([ImportMany] IEnumerable<ITypedMessageRequestHandler> handlers)
      : base(IpcProtocols.TypedMessage) {
      _handlers = handlers;
    }

    public override IpcResponse Process(IpcRequest request) {
      var typedRequest = (TypedRequest)request.Data;

      var handler = _handlers.FirstOrDefault(x => x.CanProcess(typedRequest));
      if (handler == null) {
        throw new InvalidOperationException(string.Format("No TypedMessage handler for request of type {0}",
                                                          request.GetType().Name));
      }

      var typedResponse = handler.Process(typedRequest);

      return new IpcResponse {
        RequestId = request.RequestId,
        Protocol = request.Protocol,
        Data = typedResponse
      };
    }
  }
}
