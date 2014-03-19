// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.IO;
using ProtoBuf;

namespace VsChromium.Core.Ipc.ProtoBuf {
  [Export(typeof(IProtoBufSerializer))]
  public class ProtoBufSerializer : IProtoBufSerializer {
    public void Serialize(Stream stream, IpcMessage message) {
      // Note: We need "WithLengthPrefix" method to enable serialization over
      //  a "live" stream, such as "NetworkStream".
      Serializer.SerializeWithLengthPrefix(stream, message, PrefixStyle.Base128);
      //Serializer.Serialize(stream, message);
    }

    public IpcMessage Deserialize(Stream stream) {
      // Note: We need "WithLengthPrefix" method to enable serialization over
      //  a "live" stream, such as "NetworkStream".
      return Serializer.DeserializeWithLengthPrefix<IpcMessage>(stream, PrefixStyle.Base128);
      //return Serializer.Deserialize<IpcMessage>(stream);
    }
  }
}
