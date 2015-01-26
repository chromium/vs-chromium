// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Threads;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class SourceExplorerController : ISourceExplorerController {
    private const int SearchFileNamesMaxResults = 2000;
    private const int SearchDirectoryNamesMaxResults = 2000;
    private const int SearchTextMaxResults = 10000;
    private const int SearchTextExpandMaxResults = 25;
    private static class OperationsIds {
      public const string FileContentsSearch = "files-contents-search";
      public const string DirectoryNamesSearch = "directory-names-search";
      public const string FileNamesSearch = "file-names-search";
    }

    private readonly SourceExplorerControl _control;
    private readonly IUIRequestProcessor _uiRequestProcessor;
    private readonly IProgressBarTracker _progressBarTracker;
    private readonly IStandarImageSourceFactory _standarImageSourceFactory;
    private readonly IWindowsExplorer _windowsExplorer;
    private readonly IClipboard _clipboard;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IOpenDocumentHelper _openDocumentHelper;

    /// <summary>
    /// For generating unique id n progress bar tracker.
    /// </summary>
    private int _operationSequenceId;

    public SourceExplorerController(
      SourceExplorerControl control,
      IUIRequestProcessor uiRequestProcessor,
      IProgressBarTracker progressBarTracker,
      IStandarImageSourceFactory standarImageSourceFactory,
      IWindowsExplorer windowsExplorer,
      IClipboard clipboard,
      ISynchronizationContextProvider synchronizationContextProvider,
      IOpenDocumentHelper openDocumentHelper) {
      _control = control;
      _uiRequestProcessor = uiRequestProcessor;
      _progressBarTracker = progressBarTracker;
      _standarImageSourceFactory = standarImageSourceFactory;
      _windowsExplorer = windowsExplorer;
      _clipboard = clipboard;
      _synchronizationContextProvider = synchronizationContextProvider;
      _openDocumentHelper = openDocumentHelper;
    }

    public SourceExplorerViewModel ViewModel { get { return _control.ViewModel; } }
    public IUIRequestProcessor UIRequestProcessor { get { return _uiRequestProcessor; } }
    public IStandarImageSourceFactory StandarImageSourceFactory { get { return _standarImageSourceFactory; } }
    public IClipboard Clipboard { get { return _clipboard; } }
    public IWindowsExplorer WindowsExplorer { get { return _windowsExplorer; } }
    public ISynchronizationContextProvider SynchronizationContextProvider { get { return _synchronizationContextProvider; } }
    public IOpenDocumentHelper OpenDocumentHelper { get { return _openDocumentHelper; } }

    public void NavigateToFile(FileEntryViewModel fileEntry, Span? span) {
      // Using "Post" is important: it allows the newly opened document to
      // receive the focus.
      SynchronizationContextProvider.UIContext.Post(() =>
        OpenDocumentHelper.OpenDocument(fileEntry.Path, _ => span));
    }

    /// <summary>
    /// Find the directory entry in the FileSystemTree corresponding to a directory
    /// entry containing a relative path.
    /// </summary>
    private DirectoryEntryViewModel FindDirectoryEntry(DirectoryEntryViewModel relativePath) {
      // If the view model is displaying the file system tree, don't do anything.
      if (ReferenceEquals(ViewModel.ActiveRootNodes, ViewModel.FileSystemTreeNodes))
        return null;

      // Find the top level entry of the relative path
      var topLevelEntry = GetChromiumRoot(relativePath);
      Debug.Assert(topLevelEntry != null);

      // Find the corresponding top level entry in the FileSystemTree nodes.
      var fileSystemTreeEntry = ViewModel.FileSystemTreeNodes
        .OfType<DirectoryEntryViewModel>()
        .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, topLevelEntry.Name));
      if (fileSystemTreeEntry == null)
        return null;

      // Descend the FileSystemTree nodes hierarchy as we split the directory name.
      foreach (var childName in relativePath.Name.Split(Path.DirectorySeparatorChar)) {
        // First try without forcing loading the lazy loaded entries.
        var childViewModel = fileSystemTreeEntry
          .Children
          .OfType<DirectoryEntryViewModel>()
          .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, childName));

        // Try again by forcing loading the lazy loaded entries.
        if (childViewModel == null) {
          fileSystemTreeEntry.EnsureAllChildrenLoaded();
          childViewModel = fileSystemTreeEntry
            .Children
            .OfType<DirectoryEntryViewModel>()
            .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, childName));
          if (childViewModel == null)
            return null;
        }

        fileSystemTreeEntry = childViewModel;
      }
      return fileSystemTreeEntry;
    }

    /// <summary>
    /// Return the top level entry parent of <paramref name="directoryEntry"/>
    /// </summary>
    private DirectoryEntryViewModel GetChromiumRoot(DirectoryEntryViewModel directoryEntry) {
      for (TreeViewItemViewModel current = directoryEntry; current != null; current = current.ParentViewModel) {
        if (current.ParentViewModel is RootTreeViewItemViewModel) {
          // Maybe "null" if top level node is not a directory.
          return current as DirectoryEntryViewModel;
        }
      }
      return null;
    }

    /// <summary>
    /// Navigate to the FileSystemTree directory entry corresponding to
    /// <paramref name="directoryEntry"/>. This is a no-op if the FileSystemTree
    /// is already the currently active ViewModel.
    /// </summary>
    public void NavigateToDirectory(DirectoryEntryViewModel directoryEntry) {
      var entry = FindDirectoryEntry(directoryEntry);
      ViewModel.SwitchToFileSystemTree();
      BringItemViewModelToView(entry);
    }

    public void BringItemViewModelToView(TreeViewItemViewModel item) {
      // We look for the tree view item corresponding to "item", swallowing
      // the "BringIntoView" request to avoid flickering as we descend into
      // the virtual tree and realize the sub-panels at each level.
      _control.SwallowsRequestBringIntoView(true);
      var treeViewItem = SelectTreeViewItem(item, _control.FileTreeView);

      // If we found it, allow the "BringIntoView" requests to be handled
      // and ask the tree view item to bring itself into view.
      // Note: The "BrinIntoView" call is a no-op if the tree view item
      // is already visible.
      if (treeViewItem != null) {
        _control.SwallowsRequestBringIntoView(false);
        treeViewItem.BringIntoView();
        _control.SwallowsRequestBringIntoView(true);
      }
    }

    public TreeViewItem SelectTreeViewItem(TreeViewItemViewModel item, TreeView treeView) {
      TreeViewItem result = null;
      Logger.WrapActionInvocation(() => {
        result = WpfUtilities.SelectTreeViewItem(treeView, item);
      });
      return result;
    }

    public bool ExecutedOpenCommandForItem(TreeViewItemViewModel tvi) {
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

    private class SearchWorkerParams {
      /// <summary>
      /// Simple short name of the operation (for debugging only).
      /// </summary>
      public string OperationName { get; set; }
      /// <summary>
      /// Short description of the operation (for display in status bar
      /// progress)
      /// </summary>
      public string HintText { get; set; }
      /// <summary>
      /// The request to sent to the server
      /// </summary>
      public TypedRequest TypedRequest { get; set; }
      /// <summary>
      /// Amount of time to wait before sending the request to the server.
      /// </summary>
      public TimeSpan Delay { get; set; }
      /// <summary>
      /// Lambda invoked when the response to the request has been successfully
      /// received from the server.
      /// </summary>
      public Action<TypedResponse, Stopwatch> ProcessResponse { get; set; }
    }

    private void SearchWorker(SearchWorkerParams workerParams) {
      var id = Interlocked.Increment(ref _operationSequenceId);
      var progressId = string.Format("{0}-{1}", workerParams.OperationName, id);
      var sw = new Stopwatch();
      var request = new UIRequest {
        // Note: Having a single ID for all searches ensures previous search
        // requests are superseeded.
        Id = "MetaSearch",
        Request = workerParams.TypedRequest,
        Delay = workerParams.Delay,
        OnSend = () => {
          sw.Start();
          _progressBarTracker.Start(progressId, workerParams.HintText);
        },
        OnReceive = () => {
          sw.Stop();
          _progressBarTracker.Stop(progressId);
        },
        OnSuccess = typedResponse => {
          workerParams.ProcessResponse(typedResponse, sw);
        },
        OnError = errorResponse => {
          ViewModel.SetErrorResponse(errorResponse);
        }
      };

      _uiRequestProcessor.Post(request);
    }

    public void SearchFilesNames(string searchPattern) {
      SearchWorker(new SearchWorkerParams {
        OperationName = OperationsIds.FileNamesSearch,
        HintText = "Searching for matching file names...",
        Delay = TimeSpan.FromSeconds(0.02),
        TypedRequest = new SearchFileNamesRequest {
          SearchParams = new SearchParams {
            SearchString = searchPattern,
            MaxResults = SearchFileNamesMaxResults,
            MatchCase = ViewModel.MatchCase,
            IncludeSymLinks = ViewModel.IncludeSymLinks,
            Re2 = ViewModel.UseRe2Regex,
            Regex = ViewModel.UseRegex,
          }
        },
        ProcessResponse = (typedResponse, stopwatch) => {
          var response = ((SearchFileNamesResponse)typedResponse);
          var msg = string.Format("Found {0:n0} file names among {1:n0} ({2:0.00} seconds) matching pattern \"{3}\"",
            response.HitCount,
            response.TotalCount,
            stopwatch.Elapsed.TotalSeconds,
            searchPattern);
          ViewModel.SetFileNamesSearchResult(response.SearchResult, msg, true);
        }
      });
    }

    public void SearchDirectoryNames(string searchPattern) {
      SearchWorker(new SearchWorkerParams {
        OperationName = OperationsIds.DirectoryNamesSearch,
        HintText = "Searching for matching directory names...",
        Delay = TimeSpan.FromSeconds(0.02),
        TypedRequest = new SearchDirectoryNamesRequest {
          SearchParams = new SearchParams {
            SearchString = searchPattern,
            MaxResults = SearchDirectoryNamesMaxResults,
            MatchCase = ViewModel.MatchCase,
            IncludeSymLinks = ViewModel.IncludeSymLinks,
            Re2 = ViewModel.UseRe2Regex,
            Regex = ViewModel.UseRegex,
          }
        },
        ProcessResponse = (typedResponse, stopwatch) => {
          var response = ((SearchDirectoryNamesResponse)typedResponse);
          var msg = string.Format("Found {0:n0} folder names among {1:n0} ({2:0.00} seconds) matching pattern \"{3}\"",
            response.HitCount,
            response.TotalCount,
            stopwatch.Elapsed.TotalSeconds,
            searchPattern);
          ViewModel.SetDirectoryNamesSearchResult(response.SearchResult, msg, true);
        }
      });
    }

    public void SearchText(string searchPattern) {
      SearchWorker(new SearchWorkerParams {
        OperationName = OperationsIds.FileContentsSearch,
        HintText = "Searching for matching text in files...",
        Delay = TimeSpan.FromSeconds(0.02),
        TypedRequest = new SearchTextRequest {
          SearchParams = new SearchParams {
            SearchString = searchPattern,
            MaxResults = SearchTextMaxResults,
            MatchCase = ViewModel.MatchCase,
            IncludeSymLinks = ViewModel.IncludeSymLinks,
            Re2 = ViewModel.UseRe2Regex,
            Regex = ViewModel.UseRegex,
          }
        },
        ProcessResponse = (typedResponse, stopwatch) => {
          var response = ((SearchTextResponse)typedResponse);
          var msg = string.Format("Found {0:n0} results among {1:n0} files ({2:0.00} seconds) matching text \"{3}\"",
            response.HitCount,
            response.SearchedFileCount,
            stopwatch.Elapsed.TotalSeconds,
            searchPattern);
          bool expandAll = response.HitCount < SearchTextExpandMaxResults;
          ViewModel.SetTextSearchResult(response.SearchResults, msg, expandAll);
          DisplayFindResuls(response, stopwatch);
        }
      });
    }

    private void DisplayFindResuls(SearchTextResponse response, Stopwatch stopwatch) {
#if false
      var componentModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
      var svc = componentModel.DefaultExportProvider.GetExportedValue<IVsEditorAdaptersFactoryService>();
      var window = _toolWindowAccessor.FindToolWindow(new Guid("0F887920-C2B6-11D2-9375-0080C747D9A0"));
      //_toolWindowAccessor.BuildExplorer
      var view = VsShellUtilities.GetTextView(window);
      var textView = svc.GetWpfTextView(view);
      var textBuffer = textView.TextBuffer;

      var writer = new StringWriter();
      writer.WriteLine("Found {0:n0} results among {1:n0} files ({2:0.00} seconds) matching text \"{3}\"",
            response.HitCount,
            response.SearchedFileCount,
            stopwatch.Elapsed.TotalSeconds,
            FileContentsSearch.Text);

      foreach (var root in response.SearchResults.Entries.OfType<DirectoryEntry>()) {
        var rootPath = root.Name;
        foreach (var file in root.Entries.OfType<FileEntry>()) {
          var path = PathHelpers.CombinePaths(rootPath, file.Name);
          foreach (var filePos in ((FilePositionsData) file.Data).Positions) {
            //writer.WriteLine("  {0}({1},{2}): ...", path, filePos.Position, filePos.Length);
            writer.WriteLine("  {0}(1): zzz", path);
          }
        }
      }

      // Make buffer non readonly
      var vsBuffer = svc.GetBufferAdapter(textBuffer);
      uint flags;
      vsBuffer.GetStateFlags(out flags);
      flags = flags & ~(uint)BUFFERSTATEFLAGS.BSF_USER_READONLY;
      vsBuffer.SetStateFlags(flags);

      // Clear buffer and insert text
      var edit = textBuffer.CreateEdit();
      edit.Delete(0, edit.Snapshot.Length);
      edit.Insert(0, writer.ToString());
      edit.Apply();

      // Make buffer readonly again
      flags = flags | (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY;
      vsBuffer.SetStateFlags(flags);
#endif
    }
  }
}