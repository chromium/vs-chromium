// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  /// <summary>
  /// Exposes services required by <see cref="SourceExplorerItemViewModelBase"/> instances.
  /// </summary>
  public interface ISourceExplorerItemViewModelHost {
    IUIRequestProcessor UIRequestProcessor { get; }
    IStandarImageSourceFactory StandarImageSourceFactory { get; }
    ISynchronizationContextProvider SynchronizationContextProvider { get; }
    IOpenDocumentHelper OpenDocumentHelper { get; }
  }
}