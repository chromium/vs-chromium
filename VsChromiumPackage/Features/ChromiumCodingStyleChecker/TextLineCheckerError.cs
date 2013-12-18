// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Text;

namespace VsChromiumPackage.Features.ChromiumCodingStyleChecker {
  public class TextLineCheckerError {
    public SnapshotSpan Span { get; set; }
    public string Message { get; set; }
  }
}
