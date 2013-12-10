// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  public class GetFileExtractsRequest : TypedRequest {
    public GetFileExtractsRequest() {
      Positions = new List<FilePositionSpan>();
    }

    [ProtoMember(1)]
    public string FileName { get; set; }

    [ProtoMember(2)]
    public List<FilePositionSpan> Positions { get; set; }
  }
}
