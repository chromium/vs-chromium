// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows {
  public class LazyItemViewModel : TreeViewItemViewModel {
    public LazyItemViewModel(IStandarImageSourceFactory imageSourceFactory, TreeViewItemViewModel parent)
      : base(imageSourceFactory, parent, false) {
      Text = "(Click to expand...)";
    }

    public string Text { get; set; }
    public event Action Selected;

    protected virtual void OnSelected() {
      Action handler = Selected;
      if (handler != null)
        handler();
    }

    protected override void OnPropertyChanged(string propertyName) {
      base.OnPropertyChanged(propertyName);
      if (propertyName == "IsSelected" && IsSelected) {
        OnSelected();
      }
    }
  }
}
