// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.Search {
  public interface ICompiledTextSearchProviderFactory {
    ICompiledTextSearchContainer CreateProvider(string pattern, SearchProviderOptions searchOptions);
  }

  public class SearchProviderOptions {
    public bool MatchCase { get; set; }
    public bool MatchWholeWord { get; set; }
    public bool UseRegex { get; set; }
    public bool UseRe2Engine { get; set; }
  }
}