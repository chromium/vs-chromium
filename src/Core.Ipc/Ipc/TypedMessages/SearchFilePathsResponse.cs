// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class SearchFilePathsResponse : TypedResponse {
    public SearchFilePathsResponse() {
      // To avoid getting a "null" property if an empty collection is deserialized.
      SearchResult = new DirectoryEntry();
    }

    /// <summary>
    /// This directory entry contains one child directory entry per project
    /// searched, and each of those entries contains a list of file entries
    /// matching the search criteria.
    /// </summary>
    public DirectoryEntry SearchResult { get; set; }

    /// <summary>
    /// Total number of entries returned in |FileNames|.
    /// </summary>
    public long HitCount { get; set; }

    /// <summary>
    /// Total number of entries stored in the search index.
    /// </summary>
    public long TotalCount { get; set; }
  }
}
