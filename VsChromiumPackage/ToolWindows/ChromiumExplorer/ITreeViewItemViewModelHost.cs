// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromiumPackage.Threads;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.ToolWindows.ChromiumExplorer {
  public interface ITreeViewItemViewModelHost {
    IStandarImageSourceFactory StandarImageSourceFactory { get; }
    IUIRequestProcessor UIRequestProcessor { get; }
  }
}
