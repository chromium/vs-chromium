// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDirectoryEntriesResponse : TypedResponse {
    /// <summary>
    /// <code>null</code> if the requested directory is not found in the
    /// active file system snapshpt.
    /// </summary>
    [ProtoMember(1)]
    public DirectoryEntry DirectoryEntry { get; set; }
  }
}