// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows.Media;

namespace VsChromiumPackage.Features.ChromiumExplorer {
  public class TextItemViewModel : TreeViewItemViewModel {
    private readonly string _text;

    public TextItemViewModel(
      ITreeViewItemViewModelHost host,
      TreeViewItemViewModel parent,
      string text)
      : base(host, parent, false) {
      _text = text;
    }

    public string Text { get { return _text; } }

    public override ImageSource ImageSourcePath { get { return StandarImageSourceFactory.GetImage("TextEntry"); } }
  }
}
