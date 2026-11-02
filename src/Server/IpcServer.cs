// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Ipc;
using VsChromium.Core.Logging;
using VsChromium.Server.Ipc.TypedEvents;
using VsChromium.Server.Threads;

namespace VsChromium.Server {
  [Export(typeof(IServer))]
  public class IpcServer : IServer {
    private readonly IReceiveRequestsThread _receiveThread;
    private readonly ISendResponsesThread _sendThread;
    private readonly ITypedEventForwarder _typedEventForwarder;
    private IIpcStream _stream;

    [ImportingConstructor]
    public IpcServer(
      IReceiveRequestsThread receiveThread,
      ISendResponsesThread sendThread,
      ITypedEventForwarder typedEventForwarder) {
      _sendThread = sendThread;
      _typedEventForwarder = typedEventForwarder;
      _receiveThread = receiveThread;
    }

    public void Run(IIpcStream stream) {
      _stream = stream;

      _stream.WriteResponse(HelloWorldProtocol.Response);

      _sendThread.Start(_stream);
      _receiveThread.Start(_stream);
      _typedEventForwarder.RegisterEventHandlers();

      // Receive thread will terminate soon after the IPC stream to the client (stdin) is closed.
      // There is nothing left for the server to do other than exiting at this point.
      _receiveThread.WaitOne();
      Logger.LogInfo("Server terminating properly.");
    }
  }
}
