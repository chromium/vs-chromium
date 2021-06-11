// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Diagnostics;

namespace VsChromium.Views {
  [Export(typeof(IWindowsExplorer))]
  public class WindowsExplorer : IWindowsExplorer {
    public void OpenFolder(string path) {
      Process.Start("explorer.exe", string.Format("\"{0}\"", path));
    }

    public void OpenContainingFolder(string path) {
      Process.Start("explorer.exe", string.Format("/select,\"{0}\"", path));
    }
  }
}