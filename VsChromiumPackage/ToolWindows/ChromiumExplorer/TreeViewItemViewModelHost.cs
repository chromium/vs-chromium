// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromiumPackage.Threads;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.ToolWindows.ChromiumExplorer {
  public class TreeViewItemViewModelHost : ITreeViewItemViewModelHost {
    private readonly IStandarImageSourceFactory _standarImageSourceFactory;
    private readonly IUIRequestProcessor _uiRequestProcessor;

    public TreeViewItemViewModelHost(IStandarImageSourceFactory standarImageSourceFactory, IUIRequestProcessor uiRequestProcessor) {
      _standarImageSourceFactory = standarImageSourceFactory;
      _uiRequestProcessor = uiRequestProcessor;
    }

    public IStandarImageSourceFactory StandarImageSourceFactory { get { return _standarImageSourceFactory; } }

    public IUIRequestProcessor UIRequestProcessor { get { return _uiRequestProcessor; } }
  }
}
