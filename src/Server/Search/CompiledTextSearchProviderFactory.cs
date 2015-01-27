// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;

namespace VsChromium.Server.Search {
  [Export(typeof(ICompiledTextSearchProviderFactory))]
  public class CompiledTextSearchProviderFactory : ICompiledTextSearchProviderFactory {
    public ICompiledTextSearchProvider CreateProvider(
      string pattern,
      SearchProviderOptions searchOptions) {
      // RE2 engine requires a per-thread provider, as the current C++
      // implementation suffers from serious lock contention if a RE2 regex
      // instance is shared accross threads.
      if (searchOptions.UseRegex && searchOptions.UseRe2Engine)
        return new PerThreadCompiledTextSearchProvider(pattern, searchOptions);

      return new CompiledTextSearchProvider(pattern, searchOptions);
    }
  }
}