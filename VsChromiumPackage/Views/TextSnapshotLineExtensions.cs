// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Text;

namespace VsChromiumPackage.Views {
  public static class TextSnapshotLineExtensions {
    /// <summary>
    /// Return a fragment of a text line, safely ensuring that "start" and "end" position are within the boundaries
    /// of the line. |options| can be used to customize the behavior.
    /// </summary>
    public static TextLineFragment GetFragment(
      this ITextSnapshotLine line,
      int start,
      int end,
      TextLineFragment.Options options) {
      return TextLineFragment.Create(line, start, end, options);
    }
  }
}
