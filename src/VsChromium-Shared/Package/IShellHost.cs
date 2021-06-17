// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc;

namespace VsChromium.Package {
  public interface IShellHost {
    void ShowInfoMessageBox(string title, string message);
    void ShowErrorMessageBox(string title, ErrorResponse error);
  }
}