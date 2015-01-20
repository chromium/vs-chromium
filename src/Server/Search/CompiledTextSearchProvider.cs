// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class CompiledTextSearchProvider : ICompiledTextSearchProvider {
    private readonly AsciiCompiledTextSearch _asciiCompiledTextSearchAlgo;
    private readonly Utf16CompiledTextSearch _unicodeCompiledTextSearchAlgo;

    public CompiledTextSearchProvider(string pattern, NativeMethods.SearchOptions searchOptions) {
      _asciiCompiledTextSearchAlgo = AsciiFileContents.CreateSearchAlgo(pattern, searchOptions);
      _unicodeCompiledTextSearchAlgo = Utf16FileContents.CreateSearchAlgo(pattern, searchOptions);
    }

    public ICompiledTextSearch GetAsciiSearch() {
      return _asciiCompiledTextSearchAlgo;
    }

    public ICompiledTextSearch GetUtf16Search() {
      return _unicodeCompiledTextSearchAlgo;
    }

    public void Dispose() {
      _asciiCompiledTextSearchAlgo.Dispose();
      _unicodeCompiledTextSearchAlgo.Dispose();
    }
  }
}