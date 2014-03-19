// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.ChromiumExplorer {
  public interface ITreeViewItemViewModelHost {
    IStandarImageSourceFactory StandarImageSourceFactory { get; }
    IUIRequestProcessor UIRequestProcessor { get; }
  }
}
