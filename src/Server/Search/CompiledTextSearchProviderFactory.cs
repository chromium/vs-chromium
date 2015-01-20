// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  [Export(typeof(ICompiledTextSearchProviderFactory))]
  public class CompiledTextSearchProviderFactory : ICompiledTextSearchProviderFactory {
    public ICompiledTextSearchProvider CreateProvider(
      string pattern,
      NativeMethods.SearchOptions searchOptions) {
      return new PerThreadCompiledTextSearchProvider(pattern, searchOptions);
    }
  }
}