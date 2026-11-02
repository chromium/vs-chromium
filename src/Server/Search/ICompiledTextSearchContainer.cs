// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Gives access to the native text search engine for supported encodings.
  /// </summary>
  public interface ICompiledTextSearchContainer : IDisposable {
    /// <summary>Search in text containing only ASCII characters (bytes).</summary>
    ICompiledTextSearch GetAsciiSearch();
    /// <summary>Search in UTF-8 text containing non ASCII characters (bytes).</summary>
    ICompiledTextSearch GetUtf8Search();
    /// <summary>Search in UTF-16 text (e.g. file paths).</summary>
    ICompiledTextSearch GetUtf16Search();
  }
}
