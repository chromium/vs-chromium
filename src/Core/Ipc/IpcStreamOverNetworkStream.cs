// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Net.Sockets;
using VsChromium.Core.Ipc.ProtoBuf;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Ipc {
  public class IpcStreamOverNetworkStream : IIpcStream {
    private readonly object _readerLock = new object();
    private readonly IProtoBufSerializer _serializer;
    private readonly NetworkStream _stream;
    private readonly object _writerLock = new object();

    public IpcStreamOverNetworkStream(IProtoBufSerializer serializer, NetworkStream stream) {
      _serializer = serializer;
      _stream = stream;
    }

    public void WriteRequest(IpcRequest request) {
      WriteMessage(request);
    }

    public void WriteResponse(IpcResponse response) {
      WriteMessage(response);
    }

    public IpcResponse ReadResponse() {
      return ReadMessage<IpcResponse>();
    }

    public IpcRequest ReadRequest() {
      return ReadMessage<IpcRequest>();
    }

    private void WriteMessage(IpcMessage message) {
      lock (_writerLock) {
        _serializer.Serialize(_stream, message);
      }
    }

    private T ReadMessage<T>() where T : IpcMessage, new() {
      lock (_readerLock) {
        try {
          return (T)_serializer.Deserialize(_stream);
        }
        catch (Exception e) {
          if (IsSocketClosedException(e)) {
            Logger.Log("Socket connection was closed -- assuming normal termination.");
          } else {
            Logger.LogException(e, "Error reading string from NetworkStream");
          }
          return null;
        }
      }
    }

    private static bool IsSocketClosedException(Exception e) {
      var socketException = e.GetBaseException() as SocketException;
      if (socketException == null)
        return false;

      switch (socketException.SocketErrorCode) {
        // This happens when our peer closes his side of the connection.
        case SocketError.ConnectionReset:
        // "A blocking operation was interrupted by a call to WSACancelBlockingCall"
        // This happens when we (another thread) close our side of the connection.
        case SocketError.Interrupted:
          return true;
        default:
          Logger.LogError("Socket error code: {0}", socketException.SocketErrorCode);
          return false;
      }
    }
  }
}
