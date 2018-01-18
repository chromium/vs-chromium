// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Settings;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  /// <summary>
  /// Exposes services required by <see cref="CodeSearchItemViewModelBase"/> instances.
  /// </summary>
  public interface ICodeSearchController : IDisposable {
    IUIRequestProcessor UIRequestProcessor { get; }
    IStandarImageSourceFactory StandarImageSourceFactory { get; }
    IClipboard Clipboard { get; }
    IWindowsExplorer WindowsExplorer { get; }
    GlobalSettings GlobalSettings { get; }

    void Start();

    void RefreshFileSystemTree();
    void PauseResumeIndexing();

    void PerformSearch(bool immediate);

    void OpenFileInEditor(FileEntryViewModel fileEntry, Span? span);
    void OpenFileInEditor(FileEntryViewModel fileEntry, int lineNumber, int columnNumber, int length);
    void OpenFileInEditorWith(FileEntryViewModel fileEntry, Span? span);
    void OpenFileInEditorWith(FileEntryViewModel fileEntry, int lineNumber, int columnNumber, int length);
    void ShowInSourceExplorer(FileSystemEntryViewModel relativePathEntry);
    void BringItemViewModelToView(TreeViewItemViewModel item);
    bool ExecuteOpenCommandForItem(TreeViewItemViewModel item);

    // Search result navigation
    bool HasNextLocation();
    bool HasPreviousLocation();
    void NavigateToNextLocation();
    void NavigateToPreviousLocation();
    void CancelSearch();
  }
}