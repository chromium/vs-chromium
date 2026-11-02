// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Ipc.TypedMessages {
  public class SearchParams {
    public SearchParams() {
      MaxResults = int.MaxValue;
    }

    public string SearchString { get; set; }

    public string FilePathPattern { get; set; }

    public int MaxResults { get; set; }

    public bool MatchCase { get; set; }

    public bool MatchWholeWord { get; set; }

    public bool IncludeSymLinks { get; set; }

    public bool Regex { get; set; }
  }
}
