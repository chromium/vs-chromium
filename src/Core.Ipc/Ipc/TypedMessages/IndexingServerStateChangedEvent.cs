// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class IndexingServerStateChangedEvent : TypedEvent {
    [ProtoMember(1)]
    public IndexingServerStatus ServerStatus { get; set; }
    [ProtoMember(2)]
    public DateTime LastIndexUpdatedUtc { get; set; }
  }
}