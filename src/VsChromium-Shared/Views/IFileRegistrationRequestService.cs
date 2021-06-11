// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Text;

namespace VsChromium.Views {
  /// <summary>
  /// Register/Unregister files with server.
  /// <para>
  /// Note: Starting VS 2019 (2017?), methods can be called on any thread, not just the UI thread.
  /// For example, when using "Find In Files" feature of VS, which runs on many threads in parallel.
  /// This means the implementation needs to be thread-safe.
  /// </para>
  /// </summary>
  public interface IFileRegistrationRequestService {
    void RegisterTextDocument(ITextDocument document);
    void UnregisterTextDocument(ITextDocument document);

    void RegisterFile(string path);
    void UnregisterFile(string path);
  }
}