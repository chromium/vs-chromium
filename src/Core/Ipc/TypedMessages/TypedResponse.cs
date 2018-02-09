// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  [ProtoInclude(10, typeof(DoneResponse))]
  [ProtoInclude(11, typeof(GetFileSystemResponse))]
  [ProtoInclude(12, typeof(SearchFilePathsResponse))]
  [ProtoInclude(13, typeof(SearchCodeResponse))]
  [ProtoInclude(14, typeof(GetFileSystemVersionResponse))]
  [ProtoInclude(16, typeof(GetFileExtractsResponse))]
  [ProtoInclude(17, typeof(GetDirectoryStatisticsResponse))]
  [ProtoInclude(18, typeof(RefreshFileSystemTreeResponse))]
  [ProtoInclude(19, typeof(GetDatabaseStatisticsResponse))]
  [ProtoInclude(20, typeof(PauseResumeIndexingResponse))]
  [ProtoInclude(21, typeof(GetDatabaseDetailsResponse))]
  [ProtoInclude(22, typeof(GetProjectDetailsResponse))]
  [ProtoInclude(23, typeof(GetDirectoryDetailsResponse))]
  public class TypedResponse : TypedMessage {
  }
}
