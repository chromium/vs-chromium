// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsChromiumPackage.Views {
  public interface IOpenDocumentHelper {
    bool OpenDocument(string path, Func<IVsTextView, Span?> spanProvider);
  }
}
