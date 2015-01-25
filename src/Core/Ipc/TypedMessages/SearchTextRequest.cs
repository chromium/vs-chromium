// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class SearchTextRequest : TypedRequest {
    [ProtoMember(1)]
    public SearchParams SearchParams { get; set; }
  }
}
