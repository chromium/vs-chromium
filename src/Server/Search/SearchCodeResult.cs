// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Server.Search {
  public class SearchCodeResult {
    public static SearchCodeResult Empty {
      get {
        return new SearchCodeResult { Entries = new List<FileSearchResult>() };
      }
    }

    public IList<FileSearchResult> Entries { get; set; }
    public long HitCount { get; set; }
    public long SearchedFileCount { get; set; }
    public long TotalFileCount { get; set; }
  }
}