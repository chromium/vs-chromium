// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics.CodeAnalysis;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public partial class CodeSearchController {
    private class FilePathSearchInfo {
      [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
      public string RawSearchPattern { get; set; }
      public string SearchPattern { get; set; }
      public int LineNumber { get; set; }
      public int ColumnNumber { get; set; }
    }
  }
}