// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Views {
  /// <summary>
  /// Register/Unregister files with server.
  /// </summary>
  public interface IFileRegistrationRequestService {
    void RegisterFile(string path);
    void UnregisterFile(string path);
  }
}