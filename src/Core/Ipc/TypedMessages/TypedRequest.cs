// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  [ProtoInclude(10, typeof(RegisterFileRequest))]
  [ProtoInclude(11, typeof(GetFileSystemRequest))]
  [ProtoInclude(12, typeof(SearchFileNamesRequest))]
  [ProtoInclude(13, typeof(SearchTextRequest))]
  [ProtoInclude(14, typeof(GetFileSystemVersionRequest))]
  [ProtoInclude(16, typeof(GetFileExtractsRequest))]
  [ProtoInclude(17, typeof(UnregisterFileRequest))]
  [ProtoInclude(18, typeof(GetDirectoryStatisticsRequest))]
  [ProtoInclude(19, typeof(RefreshFileSystemTreeRequest))]
  [ProtoInclude(20, typeof(GetDatabaseStatisticsRequest))]
  public class TypedRequest : TypedMessage {
  }
}
