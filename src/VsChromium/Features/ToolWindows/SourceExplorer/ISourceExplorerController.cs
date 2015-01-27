// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Text;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Threads;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  /// <summary>
  /// Exposes services required by <see cref="SourceExplorerItemViewModelBase"/> instances.
  /// </summary>
  public interface ISourceExplorerController {
    IUIRequestProcessor UIRequestProcessor { get; }
    IStandarImageSourceFactory StandarImageSourceFactory { get; }
    IClipboard Clipboard { get; }
    IWindowsExplorer WindowsExplorer { get; }

    void SetFileSystemTree(FileSystemTree tree);
    void SearchFilesNames(string searchPattern);
    void SearchDirectoryNames(string searchPattern);
    void SearchText(string searchPattern);

    void OpenFileInEditor(FileEntryViewModel fileEntry, Span? span);
    void ShowInSourceExplorer(FileSystemEntryViewModel relativePathEntry);
    void BringItemViewModelToView(TreeViewItemViewModel item);
    bool ExecuteOpenCommandForItem(TreeViewItemViewModel item);
  }
}