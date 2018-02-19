// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDirectoryEntriesMultipleResponse : TypedResponse {
    public GetDirectoryEntriesMultipleResponse() {
      DirectoryEntries = new List<OptionalDirectoryEntry>();
    }

    /// <summary>
    /// One entry per path passed in the <see cref="GetDirectoryEntriesMultipleRequest"/>.
    /// 
    /// <para>If the path does not exist in the current file system snapshot, the 
    /// <see cref="OptionalDirectoryEntry.HasValue"/> properties of the corresponding
    /// <see cref="OptionalDirectoryEntry"/> entry in the list is <code>false</code>.</para>
    /// </summary>
    [ProtoMember(1)]
    public List<OptionalDirectoryEntry> DirectoryEntries { get; set; }
  }
}