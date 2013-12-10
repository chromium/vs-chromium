// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumPackage.Threads {
  /// <summary>
  /// Post and delay requests from UI thread to the server.
  /// </summary>
  public interface IUIRequestProcessor {
    void Post(UIRequest request);
  }
}
