// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Win32.Memory;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class SearchContentsData {
    public string Text { get; set; }
    public SafeHGlobalHandle AsciiTextPtr { get; set; }
    public SafeHGlobalHandle UniTextPtr { get; set; }
    public AsciiStringSearchAlgorithm AsciiStringSearchAlgo { get; set; }
  }
}
