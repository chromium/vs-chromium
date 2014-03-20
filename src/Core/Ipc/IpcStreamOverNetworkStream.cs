// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Net.Sockets;
using VsChromium.Core.Ipc.ProtoBuf;

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
          Logger.LogException(e, "Error reading string from NetworkStream");
          return null;
        }
      }
    }
  }
}
