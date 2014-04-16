// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class SearchFileContentsResponse : TypedResponse {
    public SearchFileContentsResponse() {
      // To avoid getting "null" property if empty collection deserialized using protobuf.
      SearchResults = new DirectoryEntry();
    }

    /// <summary>
    /// This directory entry contains one child directory entry per project
    /// searched, and each of those entries containa a list of file entries
    /// matching the search criteria. Each file entry contains a list of
    /// FilePositionSpan in the <see cref="FileEntry.Data"/> property.
    /// </summary>
    [ProtoMember(1)]
    public DirectoryEntry SearchResults { get; set; }

    /// <summary>
    /// Total number of file spans returned in "SearchResults".
    /// </summary>
    [ProtoMember(2)]
    public long HitCount { get; set; }

    /// <summary>
    /// Total number of files searched before reaching "MaxResults"
    /// </summary>
    [ProtoMember(3)]
    public long SearchedFileCount { get; set; }

    /// <summary>
    /// Total number of files stored in the search index.
    /// </summary>
    [ProtoMember(4)]
    public long TotalFileCount { get; set; }
  }
}
