// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class SearchCodeResponse : TypedResponse {
    public SearchCodeResponse() {
      // To avoid getting a "null" property if an empty collection is deserialized.
      SearchResults = new DirectoryEntry();
    }

    /// <summary>
    /// This directory entry contains one child directory entry per project
    /// searched, and each of those entries containa a list of file entries
    /// matching the search criteria. Each file entry contains a list of
    /// FilePositionSpan in the <see cref="FileEntry.Data"/> property.
    /// </summary>
    public DirectoryEntry SearchResults { get; set; }

    /// <summary>
    /// Total number of file spans returned in "SearchResults".
    /// </summary>
    public long HitCount { get; set; }

    /// <summary>
    /// Total number of files searched before reaching "MaxResults"
    /// </summary>
    public long SearchedFileCount { get; set; }

    /// <summary>
    /// Total number of files stored in the search index.
    /// </summary>
    public long TotalFileCount { get; set; }
  }
}
