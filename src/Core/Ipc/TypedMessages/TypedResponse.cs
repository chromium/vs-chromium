// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  [ProtoInclude(10, typeof(DoneResponse))]
  [ProtoInclude(11, typeof(GetFileSystemResponse))]
  [ProtoInclude(12, typeof(SearchFileNamesResponse))]
  [ProtoInclude(13, typeof(SearchTextResponse))]
  [ProtoInclude(14, typeof(GetFileSystemVersionResponse))]
  [ProtoInclude(15, typeof(SearchDirectoryNamesResponse))]
  [ProtoInclude(16, typeof(GetFileExtractsResponse))]
  [ProtoInclude(17, typeof(GetDirectoryStatisticsResponse))]
  public class TypedResponse : TypedMessage {
  }
}
