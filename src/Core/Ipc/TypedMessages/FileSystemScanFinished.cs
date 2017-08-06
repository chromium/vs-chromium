// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class FileSystemScanFinished : PairedTypedEvent {
    [ProtoMember(1)]
    public bool IsCancelled { get; set; }

    [ProtoMember(2)]
    public int OldVersion { get; set; }

    [ProtoMember(3)]
    public int NewVersion { get; set; }
  }
}
