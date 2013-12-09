// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;

namespace VsChromiumCore.Ipc.ProtoBuf {
  public interface IProtoBufSerializer {
    void Serialize(Stream stream, IpcMessage message);
    IpcMessage Deserialize(Stream stream);
  }
}
