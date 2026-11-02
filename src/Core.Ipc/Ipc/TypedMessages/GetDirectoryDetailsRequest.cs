// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDirectoryDetailsRequest : TypedRequest {
    [ProtoMember(1)]
    public string Path { get; set; }

    [ProtoMember(2)]
    public int MaxFilesByExtensionDetailsCount { get; set; }

    [ProtoMember(3)]
    public int MaxLargeFilesDetailsCount { get; set; }
  }
}