// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Gives access to the native text search engine for supported encodings.
  /// </summary>
  public interface ICompiledTextSearchContainer : IDisposable {
    ICompiledTextSearch GetAsciiSearch();
    ICompiledTextSearch GetUtf16Search();
  }
}