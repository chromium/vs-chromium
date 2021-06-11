// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Wpf;

namespace VsChromium.Views {
  [Export(typeof(IClipboard))]
  public class Clipboard : IClipboard {
    public void SetText(string text) {
      System.Windows.Clipboard.SetText(text);
    }
  }
}