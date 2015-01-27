// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;

namespace VsChromium.Views {
  public interface ITextDocumentService {
    void OnDocumentOpen(ITextDocument document);
    void OnDocumentClose(ITextDocument document);

    void RegisterFile(FullPath path);
    void UnregisterFile(FullPath path);
  }
}