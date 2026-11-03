// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows.Media;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows {
  public class TextItemViewModel : TreeViewItemViewModel {
    private readonly string _text;

    public TextItemViewModel(IStandarImageSourceFactory imageSourceFactory, TreeViewItemViewModel parent, string text)
      : base(imageSourceFactory, parent, false) {
      _text = text;
    }

    public string Text {
      get {
        return _text;
      }
    }

    public override ImageSource ImageSourcePath {
      get {
        return StandarImageSourceFactory.GetImage("TextEntry");
      }
    }
  }
}
