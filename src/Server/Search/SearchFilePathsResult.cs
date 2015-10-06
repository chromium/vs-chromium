// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.Search {
  public class SearchFilePathsResult {
    public static SearchFilePathsResult Empty { get { return new SearchFilePathsResult { FileNames = new List<FileName>() }; } }

    public IList<FileName> FileNames { get; set; }
    public long TotalCount { get; set; }
  }
}