// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Features.BuildOutputAnalyzer {
  public class BuildOutputSpan {
    public string Text { get; set; }
    public int Index { get; set; }
    public int Length { get; set; }
    public string FileName { get; set; }
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
  }
}