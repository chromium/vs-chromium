// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  [ProtoInclude(10, typeof(FileSystemTreeComputing))]
  [ProtoInclude(11, typeof(FileSystemTreeComputed))]
  [ProtoInclude(12, typeof(SearchEngineFilesLoading))]
  [ProtoInclude(13, typeof(SearchEngineFilesLoaded))]
  public class PairedTypedEvent : TypedEvent {
    [ProtoMember(1)]
    public long OperationId { get; set; }
  }
}
