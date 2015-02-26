// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Text;

namespace VsChromium.Views {
  /// <summary>
  /// Register/Unregister files with server.
  /// </summary>
  public interface IFileRegistrationRequestService {
    void RegisterTextDocument(ITextDocument document);
    void UnregisterTextDocument(ITextDocument document);

    void RegisterFile(string path);
    void UnregisterFile(string path);
  }
}