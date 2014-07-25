// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.VisualStudio.ComponentModelHost;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Features.AutoUpdate;
using VsChromium.ServerProxy;
using VsChromium.Threads;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  /// <summary>
  /// Interaction logic for SourceExplorerControl.xaml
  /// </summary>
  public partial class SourceExplorerControl : UserControl {
    private const int _searchDirectoryNamesMaxResults = 2000;
    private const int _searchFileNamesMaxResults = 2000;
    private const int _searchFileContentsMaxResults = 10000;

    private readonly IProgressBarTracker _progressBarTracker;
    private IStatusBar _statusBar;
    private ITypedRequestProcessProxy _typedRequestProcessProxy;
    private IUIRequestProcessor _uiRequestProcessor;
    private bool _swallowsRequestBringIntoView = true;

    public SourceExplorerControl() {
      InitializeComponent();

      base.DataContext = new SourceExplorerViewModel();

      _progressBarTracker = new ProgressBarTracker(ProgressBar);

      InitComboBox(FileNamesSearch, new ComboBoxInfo {
        SearchFunction = SearchFilesNames
      });
      InitComboBox(DirectoryNamesSearch, new ComboBoxInfo {
        SearchFunction = SearchDirectoryNames
      });
      InitComboBox(FileContentsSearch, new ComboBoxInfo {
        SearchFunction = SearchFileContents
      });
    }

    public SourceExplorerViewModel ViewModel { get { return (SourceExplorerViewModel)DataContext; } }

    public UpdateInfo UpdateInfo {
      get { return ViewModel.UpdateInfo; }
      set { ViewModel.UpdateInfo = value; }
    }

    public void OnToolWindowCreated(IServiceProvider serviceProvider) {
      var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));

      _uiRequestProcessor = componentModel.DefaultExportProvider.GetExportedValue<IUIRequestProcessor>();
      _statusBar = componentModel.DefaultExportProvider.GetExportedValue<IStatusBar>();
      _typedRequestProcessProxy = componentModel.DefaultExportProvider.GetExportedValue<ITypedRequestProcessProxy>();
      _typedRequestProcessProxy.EventReceived += TypedRequestProcessProxy_EventReceived;

      var standarImageSourceFactory = componentModel.DefaultExportProvider.GetExportedValue<IStandarImageSourceFactory>();
      var clipboard = componentModel.DefaultExportProvider.GetExportedValue<IClipboard>();
      var windowsExplorer = componentModel.DefaultExportProvider.GetExportedValue<IWindowsExplorer>();
      var openDocumentHelper = componentModel.DefaultExportProvider.GetExportedValue<IOpenDocumentHelper>();
      var synchronizationContextProvider = componentModel.DefaultExportProvider.GetExportedValue<ISynchronizationContextProvider>();
      var host = new SourceExplorerViewModelHost(this, _uiRequestProcessor, standarImageSourceFactory, windowsExplorer, clipboard, synchronizationContextProvider, openDocumentHelper);
      ViewModel.SetHost(host);

      ViewModel.OnToolWindowCreated(serviceProvider);

      FetchFilesystemTree();
    }

    private void InitComboBox(EditableComboBox comboBox, ComboBoxInfo info) {
      comboBox.DataContext = new StringListViewModel();
      comboBox.TextChanged += (s, e) => info.SearchFunction();
      comboBox.KeyDown += (s, e) => {
        if (e.Key == Key.Return || e.Key == Key.Enter)
          info.SearchFunction();
      };
      comboBox.PrePreviewKeyDown += (s, e) => {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key == Key.Down) {
          FileTreeView.Focus();
          e.Handled = true;
        }
      };
    }

    private void TypedRequestProcessProxy_EventReceived(TypedEvent typedEvent) {
      DispatchFileSystemTreeComputing(typedEvent);
      DispatchFileSystemTreeComputed(typedEvent);
      DispatchSearchEngineFilesLoading(typedEvent);
      DispatchSearchEngineFilesLoaded(typedEvent);
      DispatchProgressReport(typedEvent);
    }

    private void DispatchProgressReport(TypedEvent typedEvent) {
      var @event = typedEvent as ProgressReportEvent;
      if (@event != null) {
        Wpf.WpfUtilities.Post(this,
                              () => _statusBar.ReportProgress(@event.DisplayText, @event.Completed, @event.Total));
      }
    }

    private void DispatchFileSystemTreeComputing(TypedEvent typedEvent) {
      var @event = typedEvent as FileSystemTreeComputing;
      if (@event != null) {
        Wpf.WpfUtilities.Post(this, () => {
          Logger.Log("FileSystemTree is being computed on server.");
          _progressBarTracker.Start(OperationsIds.FileSystemTreeComputing,
                                    "Loading files and directory names from file system.");
          ViewModel.FileSystemTreeComputing();
        });
      }
    }

    private void DispatchFileSystemTreeComputed(TypedEvent typedEvent) {
      var @event = typedEvent as FileSystemTreeComputed;
      if (@event != null) {
        Wpf.WpfUtilities.Post(this, () => {
          _progressBarTracker.Stop(OperationsIds.FileSystemTreeComputing);
          if (@event.Error != null) {
            ViewModel.SetErrorResponse(@event.Error);
            return;
          }
          Logger.Log("New FileSystemTree bas been computed on server: version={0}.", @event.NewVersion);
          FetchFilesystemTree();
        });
      }
    }

    private void DispatchSearchEngineFilesLoading(TypedEvent typedEvent) {
      var @event = typedEvent as SearchEngineFilesLoading;
      if (@event != null) {
        Wpf.WpfUtilities.Post(this, () => {
          Logger.Log("Search engine is loading files on server.");
          _progressBarTracker.Start(OperationsIds.FilesLoading, "Loading files contents from file system.");
        });
      }
    }

    private void DispatchSearchEngineFilesLoaded(TypedEvent typedEvent) {
      var @event = typedEvent as SearchEngineFilesLoaded;
      if (@event != null) {
        Wpf.WpfUtilities.Post(this, () => {
          _progressBarTracker.Stop(OperationsIds.FilesLoading);
          if (@event.Error != null) {
            ViewModel.SetErrorResponse(@event.Error);
            return;
          }
          Logger.Log("Search engine is done loading files on server.");
        });
      }
    }

    private void FetchFilesystemTree() {
      var request = new UIRequest() {
        Id = "GetFileSystemRequest",
        TypedRequest = new GetFileSystemRequest {
        },
        SuccessCallback = (typedResponse) => {
          var response = (GetFileSystemResponse)typedResponse;
          ViewModel.SetFileSystemTree(response.Tree);
        },
        ErrorCallback = (errorResponse) => {
          ViewModel.SetErrorResponse(errorResponse);
        }
      };

      _uiRequestProcessor.Post(request);
    }

    private void MetaSearch(SearchMetadata metadata) {
      var sw = new Stopwatch();
      var request = new UIRequest() {
        Id = "MetaSearch",
        TypedRequest = metadata.TypedRequest,
        Delay = metadata.Delay,
        OnBeforeRun = () => {
          sw.Start();
          _progressBarTracker.Start(metadata.OperationId, metadata.HintText);
        },
        OnAfterRun = () => {
          sw.Stop();
          _progressBarTracker.Stop(metadata.OperationId);
        },
        SuccessCallback = typedResponse => {
          metadata.ProcessResponse(typedResponse, sw);
        },
        ErrorCallback = errorResponse => {
          ViewModel.SetErrorResponse(errorResponse);
        }
      };

      _uiRequestProcessor.Post(request);
    }

    private void SearchFilesNames() {
      MetaSearch(new SearchMetadata {
        Delay = TimeSpan.FromSeconds(0.02),
        HintText = "Searching for matching file names...",
        OperationId = OperationsIds.FileNamesSearch,
        TypedRequest = new SearchFileNamesRequest {
          SearchParams = new SearchParams {
            SearchString = FileNamesSearch.Text,
            MaxResults = _searchFileNamesMaxResults,
            MatchCase = ViewModel.MatchCase
          }
        },
        ProcessResponse = (typedResponse, stopwatch) => {
          var response = ((SearchFileNamesResponse)typedResponse);
          var msg = string.Format("Found {0:n0} files among {1:n0} ({2:0.00} seconds) matching pattern \"{3}\"",
            response.HitCount,
            response.TotalCount,
            stopwatch.Elapsed.TotalSeconds,
            FileNamesSearch.Text);
          ViewModel.SetFileNamesSearchResult(response.SearchResult, msg, true);
        }
      });
    }

    private void SearchDirectoryNames() {
      MetaSearch(new SearchMetadata {
        Delay = TimeSpan.FromSeconds(0.02),
        HintText = "Searching for matching directory names...",
        OperationId = OperationsIds.DirectoryNamesSearch,
        TypedRequest = new SearchDirectoryNamesRequest {
          SearchParams = new SearchParams {
            SearchString = DirectoryNamesSearch.Text,
            MaxResults = _searchDirectoryNamesMaxResults,
            MatchCase = ViewModel.MatchCase
          }
        },
        ProcessResponse = (typedResponse, stopwatch) => {
          var response = ((SearchDirectoryNamesResponse)typedResponse);
          var msg = string.Format("Found {0:n0} directories among {1:n0} ({2:0.00} seconds) matching pattern \"{3}\"",
            response.HitCount,
            response.TotalCount,
            stopwatch.Elapsed.TotalSeconds,
            DirectoryNamesSearch.Text);
          ViewModel.SetDirectoryNamesSearchResult(response.SearchResult, msg, true);
        }
      });
    }

    private void SearchFileContents() {
      MetaSearch(new SearchMetadata {
        Delay = TimeSpan.FromSeconds(0.02),
        HintText = "Searching for matching text in files...",
        OperationId = OperationsIds.FileContentsSearch,
        TypedRequest = new SearchFileContentsRequest {
          SearchParams = new SearchParams {
            SearchString = FileContentsSearch.Text,
            MaxResults = _searchFileContentsMaxResults,
            MatchCase = ViewModel.MatchCase
          }
        },
        ProcessResponse = (typedResponse, stopwatch) => {
          var response = ((SearchFileContentsResponse)typedResponse);
          var msg = string.Format("Found {0:n0} results among {1:n0} files ({2:0.00} seconds) matching text \"{3}\"",
            response.HitCount,
            response.SearchedFileCount,
            stopwatch.Elapsed.TotalSeconds,
            FileContentsSearch.Text);
          bool expandAll = response.HitCount < 25;
          ViewModel.SetFileContentsSearchResult(response.SearchResults, msg, expandAll);
        }
      });
    }

    public bool NavigateFromSelectedItem(TreeViewItemViewModel tvi) {
      if (tvi == null)
        return false;

      if (!tvi.IsSelected)
        return false;

      {
        var filePosition = tvi as FilePositionViewModel;
        if (filePosition != null) {
          filePosition.OpenCommand.Execute(filePosition);
          return true;
        }
      }

      {
        var fileEntry = tvi as FileEntryViewModel;
        if (fileEntry != null) {
          fileEntry.OpenCommand.Execute(fileEntry);
          return true;
        }
      }

      {
        var directoryEntry = tvi as DirectoryEntryViewModel;
        if (directoryEntry != null) {
          directoryEntry.OpenCommand.Execute(directoryEntry);
          return true;
        }
      }

      return false;
    }

    private class ComboBoxInfo {
      public Action SearchFunction { get; set; }
    }

    private static class OperationsIds {
      public const string FileSystemTreeComputing = "file-system-collecting";
      public const string FilesLoading = "files-loading";
      public const string FileContentsSearch = "files-contents-search";
      public const string DirectoryNamesSearch = "directory-names-search";
      public const string FileNamesSearch = "file-names-search";
    }

    private class SearchMetadata {
      public TypedRequest TypedRequest { get; set; }
      public Action<TypedResponse, Stopwatch> ProcessResponse { get; set; }
      public TimeSpan Delay { get; set; }
      public string OperationId { get; set; }
      public string HintText { get; set; }
    }

    public void SwallowsRequestBringIntoView(bool value) {
      _swallowsRequestBringIntoView = value;
    }


    #region WPF Event handlers

    private void CancelSearchButton_Click(object sender, RoutedEventArgs e) {
      ViewModel.SwitchToFileSystemTree();
    }

    private void TreeViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) {
      var tvi = sender as TreeViewItem;
      if (tvi == null)
        return;

      if (!tvi.IsSelected)
        return;

      if (NavigateFromSelectedItem(tvi.DataContext as TreeViewItemViewModel))
        e.Handled = true;
    }

    private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
      // This prevents the tree view for scrolling horizontally to make the
      // selected item as visibile as possible. This is useful for
      // "SearchFileContents", as text extracts are usually wide enough to make
      // tree view navigation annoying when they are selected.
      e.Handled = _swallowsRequestBringIntoView;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
      // Open the default web browser to the update URL.
      Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
      e.Handled = true;
    }

    private void FileTreeView_OnPreviewKeyDown(object sender, KeyEventArgs e) {
      if (e.Key == Key.Return) {
        e.Handled = NavigateFromSelectedItem(FileTreeView.SelectedItem as TreeViewItemViewModel);
      }
    }

    /// <summary>
    /// Ensures the item right-clicked on is selected before showing the context
    /// menu.
    /// </summary>
    private void FileTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
      var source = e.OriginalSource as DependencyObject;
      if (source == null)
        return;

      var treeViewItem = VisualTreeGetParentOfType<TreeViewItem>(source);
      if (treeViewItem == null)
        return;

      treeViewItem.Focus();
      e.Handled = true;
    }

    #endregion

    /// <summary>
    /// Returns the first parent of <paramref name="source"/> of type
    /// <typeparamref name="T"/>.
    /// </summary>
    static T VisualTreeGetParentOfType<T>(DependencyObject source) where T : DependencyObject {
      while (source != null) {
        if (source is T)
          return (T)source;
        source = VisualTreeHelper.GetParent(source);
      }
      return null;
    }
  }
}
