// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  [ProtoInclude(10, typeof(AddFileNameRequest))]
  [ProtoInclude(11, typeof(GetFileSystemRequest))]
  [ProtoInclude(12, typeof(SearchFileNamesRequest))]
  [ProtoInclude(13, typeof(SearchFileContentsRequest))]
  [ProtoInclude(14, typeof(GetFileSystemVersionRequest))]
  [ProtoInclude(15, typeof(SearchDirectoryNamesRequest))]
  [ProtoInclude(16, typeof(GetFileExtractsRequest))]
  [ProtoInclude(17, typeof(RemoveFileNameRequest))]
  public class TypedRequest : TypedMessage {
  }
}
