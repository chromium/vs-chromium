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
      _serializer = serializer;
      _sendThread = sendThread;
      _typedEventForwarder = typedEventForwarder;
      _receiveThread = receiveThread;
    }

    public void Run(int port) {
      var client = new TcpClient();
      client.NoDelay = true;
      client.Connect(IPAddress.Loopback, port);
      Logger.Log("Server connected to host port {0}.", port);
      _stream = new IpcStreamOverNetworkStream(_serializer, client.GetStream());

      _stream.WriteResponse(HelloWorldProtocol.Response);

      _sendThread.Start(_stream);
      _receiveThread.Start(_stream);
      _typedEventForwarder.RegisterEventHandlers();

      // Receive thread will terminate soon after the TCP connection to the VS package is
      // closed. There is nothing left for the server to do other than exiting at this point.
      _receiveThread.WaitOne();
      Logger.Log("Server terminating properly.");
    }
  }
}
