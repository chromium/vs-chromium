// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Container for various pieces of data needed by the text search components.
  /// TODO(rpaquay): It would be nicer to make this a little bit more OO and decouple.
  /// </summary>
  public class SearchContentsData {
    public ParsedSearchString ParsedSearchString { get; set; }
    public AsciiStringSearchAlgorithm AsciiStringSearchAlgo { get; set; }
    public UTF16StringSearchAlgorithm UTF16StringSearchAlgo { get; set; }
  }
}
