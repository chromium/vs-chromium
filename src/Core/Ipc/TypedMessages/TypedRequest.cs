// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  [ProtoInclude(10, typeof(RegisterFileRequest))]
  [ProtoInclude(11, typeof(UnregisterFileRequest))]
  [ProtoInclude(12, typeof(GetFileSystemRequest))]
  [ProtoInclude(13, typeof(SearchFilePathsRequest))]
  [ProtoInclude(14, typeof(SearchCodeRequest))]
  [ProtoInclude(15, typeof(GetFileSystemVersionRequest))]
  [ProtoInclude(16, typeof(GetFileExtractsRequest))]
  [ProtoInclude(18, typeof(GetDirectoryStatisticsRequest))]
  [ProtoInclude(19, typeof(RefreshFileSystemTreeRequest))]
  [ProtoInclude(20, typeof(GetDatabaseStatisticsRequest))]
  [ProtoInclude(21, typeof(PauseIndexingRequest))]
  [ProtoInclude(22, typeof(ResumeIndexingRequest))]
  [ProtoInclude(23, typeof(GetDatabaseDetailsRequest))]
  [ProtoInclude(24, typeof(GetProjectDetailsRequest))]
  [ProtoInclude(25, typeof(GetDirectoryDetailsRequest))]
  [ProtoInclude(26, typeof(GetFileSystemTreeRequest))]
  [ProtoInclude(27, typeof(GetDirectoryEntriesRequest))]
  [ProtoInclude(28, typeof(GetDirectoryEntriesMultipleRequest))]
  public class TypedRequest : TypedMessage {
  }
}
