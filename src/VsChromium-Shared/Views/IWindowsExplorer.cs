// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Views {
  public interface IWindowsExplorer {
    void OpenFolder(string path);
    void OpenContainingFolder(string path);
  }
}