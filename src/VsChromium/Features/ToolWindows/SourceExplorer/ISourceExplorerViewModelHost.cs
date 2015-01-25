// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.Text;
using VsChromium.Threads;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  /// <summary>
  /// Exposes services required by <see cref="SourceExplorerItemViewModelBase"/> instances.
  /// </summary>
  public interface ISourceExplorerViewModelHost {
    IUIRequestProcessor UIRequestProcessor { get; }
    IStandarImageSourceFactory StandarImageSourceFactory { get; }
    IClipboard Clipboard { get; }
    IWindowsExplorer WindowsExplorer { get; }

    void NavigateToFile(FileEntryViewModel fileEntry, Span? span);
    void NavigateToDirectory(DirectoryEntryViewModel directoryEntry);
    void BringViewModelToView(TreeViewItemViewModel item);
  }
}