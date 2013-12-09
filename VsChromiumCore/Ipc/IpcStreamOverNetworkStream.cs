// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Net.Sockets;
using VsChromiumCore.Ipc.ProtoBuf;

namespace VsChromiumCore.Ipc {
  public class IpcStreamOverNetworkStream : IIpcStream {
    private readonly object _readerLock = new object();
    private readonly IProtoBufSerializer _serializer;
    private readonly NetworkStream _stream;
    private readonly object _writerLock = new object();

    public IpcStreamOverNetworkStream(IProtoBufSerializer serializer, NetworkStream stream) {
      this._serializer = serializer;
      this._stream = stream;
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
      lock (this._writerLock) {
        this._serializer.Serialize(this._stream, message);
      }
    }

    private T ReadMessage<T>() where T : IpcMessage, new() {
      lock (this._readerLock) {
        try {
          return (T)this._serializer.Deserialize(this._stream);
        }
        catch (Exception e) {
          Logger.LogException(e, "Error reading string from NetworkStream");
          return null;
        }
      }
    }
  }
}
