// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using VsChromiumCore;
using VsChromiumCore.Ipc;
using VsChromiumCore.Ipc.ProtoBuf;
using VsChromiumServer.Ipc.TypedEvents;
using VsChromiumServer.Threads;

namespace VsChromiumServer {
  [Export(typeof(IServer))]
  public class ServerOverNetworkStream : IServer {
    private readonly IReceiveRequestsThread _receiveThread;
    private readonly ISendResponsesThread _sendThread;
    private readonly IProtoBufSerializer _serializer;
    private readonly ITypedEventForwarder _typedEventForwarder;
    private IIpcStream _stream;

    [ImportingConstructor]
    public ServerOverNetworkStream(
        IProtoBufSerializer serializer,
        IReceiveRequestsThread receiveThread,
        ISendResponsesThread sendThread,
        ITypedEventForwarder typedEventForwarder) {
      this._serializer = serializer;
      this._sendThread = sendThread;
      this._typedEventForwarder = typedEventForwarder;
      this._receiveThread = receiveThread;
    }

    public void Run(int port) {
      var client = new TcpClient();
      client.NoDelay = true;
      client.Connect(IPAddress.Loopback, port);
      Logger.Log("Server connected to host port {0}.", port);
      this._stream = new IpcStreamOverNetworkStream(this._serializer, client.GetStream());

      this._stream.WriteResponse(HelloWorldProtocol.Response);

      this._sendThread.Start(this._stream);
      this._receiveThread.Start(this._stream);
      this._typedEventForwarder.RegisterEventHandlers();

      this._sendThread.WaitOne();
      this._receiveThread.WaitOne();
      Logger.Log("Server terminating properly.");
    }
  }
}
