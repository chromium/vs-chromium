// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumPackage.Classifier {
  public interface ITextLineChecker {
    bool AppliesToContentType(IContentType contentType);

    /// <summary>
    /// Classify a line if there is something to report about that line.
    /// </summary>
    IEnumerable<TextLineCheckerError> CheckLine(ITextSnapshotLine line);
  }
}
