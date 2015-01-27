// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;
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
    private readonly TaskCancellation _taskCancellation;

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
      _taskCancellation = new TaskCancellation();
    }

    public SourceExplorerViewModel ViewModel { get { return _control.ViewModel; } }
    public IUIRequestProcessor UIRequestProcessor { get { return _uiRequestProcessor; } }
    public IStandarImageSourceFactory StandarImageSourceFactory { get { return _standarImageSourceFactory; } }
    public IClipboard Clipboard { get { return _clipboard; } }
    public IWindowsExplorer WindowsExplorer { get { return _windowsExplorer; } }
    public ISynchronizationContextProvider SynchronizationContextProvider { get { return _synchronizationContextProvider; } }
    public IOpenDocumentHelper OpenDocumentHelper { get { return _openDocumentHelper; } }

    public void OpenFileInEditor(FileEntryViewModel fileEntry, Span? span) {
      // Using "Post" is important: it allows the newly opened document to
      // receive the focus.
      SynchronizationContextProvider.UIContext.Post(() =>
        OpenDocumentHelper.OpenDocument(fileEntry.Path, _ => span));
    }

    public List<TreeViewItemViewModel> CreateFileSystemTreeViewModel(FileSystemTree tree) {
      var rootNode = new RootTreeViewItemViewModel(StandarImageSourceFactory);
      var result = new List<TreeViewItemViewModel>(tree.Root
        .Entries
        .Select(x => FileSystemEntryViewModel.Create(this, rootNode, x)));
      result.ForAll(rootNode.AddChild);
      ChromiumExplorerViewModelBase.ExpandNodes(result, false);
      return result;
    }

    public List<TreeViewItemViewModel> CreateFileNamesSearchResult(DirectoryEntry fileResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(StandarImageSourceFactory);
      var result =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(StandarImageSourceFactory, rootNode, description)
        }.Concat(
          fileResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(this, rootNode, x)))
          .ToList();
      result.ForAll(rootNode.AddChild);
      ChromiumExplorerViewModelBase.ExpandNodes(result, expandAll);
      return result;
    }

    public List<TreeViewItemViewModel> CreateDirectoryNamesSearchResult(DirectoryEntry directoryResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(StandarImageSourceFactory);
      var result =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(StandarImageSourceFactory, rootNode, description)
        }.Concat(
          directoryResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(this, rootNode, x)))
          .ToList();
      result.ForAll(rootNode.AddChild);
      ChromiumExplorerViewModelBase.ExpandNodes(result, expandAll);
      return result;
    }

    public List<TreeViewItemViewModel> CreateTextSearchResultViewModel(DirectoryEntry searchResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(StandarImageSourceFactory);
      var result =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(StandarImageSourceFactory, rootNode, description)
        }.Concat(
          searchResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(this, rootNode, x)))
          .ToList();
      result.ForAll(rootNode.AddChild);
      ChromiumExplorerViewModelBase.ExpandNodes(result, expandAll);
      return result;
    }

    /// <summary>
    /// Find the directory entry in the FileSystemTree corresponding to a directory
    /// entry containing a relative path or a project root path.
    /// </summary>
    private static FileSystemEntryViewModel FindFileSystemEntryForRelativePath(
      List<TreeViewItemViewModel> fileSystemTreeNodes,
      FileSystemEntryViewModel relativePathEntry) {
      // Find the top level entry of the relative path
      var topLevelEntry = GetChromiumRoot(relativePathEntry);
      Debug.Assert(topLevelEntry != null);

      // Find the corresponding top level entry in the FileSystemTree nodes.
      var fileSystemTreeEntry = fileSystemTreeNodes
        .OfType<FileSystemEntryViewModel>()
        .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, topLevelEntry.Name));
      if (fileSystemTreeEntry == null)
        return null;

      // Special case: "relativePath" is actually a Root entry.
      if (topLevelEntry == relativePathEntry) {
        return fileSystemTreeEntry;
      }

      // Descend the FileSystemTree nodes hierarchy as we split the directory name.
      foreach (var childName in relativePathEntry.Name.Split(Path.DirectorySeparatorChar)) {
        // First try without forcing loading the lazy loaded entries.
        var childViewModel = fileSystemTreeEntry
          .Children
          .OfType<FileSystemEntryViewModel>()
          .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, childName));

        // Try again by forcing loading the lazy loaded entries.
        if (childViewModel == null) {
          fileSystemTreeEntry.EnsureAllChildrenLoaded();
          childViewModel = fileSystemTreeEntry
            .Children
            .OfType<FileSystemEntryViewModel>()
            .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, childName));
          if (childViewModel == null)
            return null;
        }

        fileSystemTreeEntry = childViewModel;
      }
      return fileSystemTreeEntry;
    }

    /// <summary>
    /// Find the directory entry in the FileSystemTree corresponding to a directory
    /// entry containing a relative path or a project root path.
    /// </summary>
    private static FileSystemEntryViewModel FindFileSystemEntryForPath(
      List<TreeViewItemViewModel> fileSystemTreeNodes,
      string path) {
      // Find the corresponding top level entry in the FileSystemTree nodes.
      var fileSystemTreeEntry = fileSystemTreeNodes
        .OfType<FileSystemEntryViewModel>()
        .FirstOrDefault(x => PathHelpers.IsPrefix(path, x.Name));
      if (fileSystemTreeEntry == null)
        return null;

      var pair = PathHelpers.SplitPath(path, fileSystemTreeEntry.Name);

      // Special case: "path" is actually a Root entry.
      if (pair.Value == "") {
        return fileSystemTreeEntry;
      }

      // Descend the FileSystemTree nodes hierarchy as we split the directory name.
      foreach (var childName in pair.Value.Split(Path.DirectorySeparatorChar)) {
        // First try without forcing loading the lazy loaded entries.
        var childViewModel = fileSystemTreeEntry
          .Children
          .OfType<FileSystemEntryViewModel>()
          .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, childName));

        // Try again by forcing loading the lazy loaded entries.
        if (childViewModel == null) {
          fileSystemTreeEntry.EnsureAllChildrenLoaded();
          childViewModel = fileSystemTreeEntry
            .Children
            .OfType<FileSystemEntryViewModel>()
            .FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, childName));
          if (childViewModel == null)
            return null;
        }

        fileSystemTreeEntry = childViewModel;
      }
      return fileSystemTreeEntry;
    }

    /// <summary>
    /// Returns the node contained in <paramref name="fileSystemTreeNodes"/>
    /// that has the exact same path as <paramref name="node"/>. This method is
    /// used to find equivalent nodes between different versions of the file
    /// system tree.
    /// </summary>
    private static TreeViewItemViewModel FindSameNode(
      List<TreeViewItemViewModel> fileSystemTreeNodes,
      TreeViewItemViewModel node) {
      var root = GetChromiumRoot(node);
      if (root == null)
        return null;

      var newRoot = fileSystemTreeNodes.FirstOrDefault(x => x.DisplayText == root.DisplayText);
      if (newRoot == null)
        return null;

      // Create stack of parent -> child for DFS search
      var stack = new Stack<TreeViewItemViewModel>();
      var item = node;
      while (item != root) {
        stack.Push(item);
        item = item.ParentViewModel;
      }

      // Process all stack elements, looking for their equivalent in
      // "fileSystemTreeEntry"
      var fileSystemTreeEntry = newRoot;
      while (stack.Count > 0) {
        var child = stack.Pop();

        // First try without forcing loading the lazy loaded entries.
        var childViewModel = fileSystemTreeEntry
          .Children
          .FirstOrDefault(x => x.DisplayText == child.DisplayText);

        // Try again by forcing loading the lazy loaded entries.
        if (childViewModel == null) {
          fileSystemTreeEntry.EnsureAllChildrenLoaded();
          childViewModel = fileSystemTreeEntry
            .Children
            .FirstOrDefault(x => x.DisplayText == child.DisplayText);
          if (childViewModel == null)
            return null;
        }

        fileSystemTreeEntry = childViewModel;
      }
      return fileSystemTreeEntry;
    }

    /// <summary>
    /// Transfer the "IsExpanded" and "IsSelected" state of the nodes from an
    /// old file system tree to a new one.
    /// </summary>
    private static void TransferFileSystemTreeState(
      List<TreeViewItemViewModel> oldFileSystemTree,
      List<TreeViewItemViewModel> newFileSystemTree) {
      var state = new FileSystemTreeState();
      oldFileSystemTree.ForEach(state.ProcessNodes);
      state.ExpandedNodes.ForAll(
        x => {
          var y = FindSameNode(newFileSystemTree, x);
          if (y != null)
            y.IsExpanded = true;
        });
      state.SelectedNodes.ForAll(
        x => {
          var y = FindSameNode(newFileSystemTree, x);
          if (y != null)
            y.IsSelected = true;
        });
    }

    public class FileSystemTreeState {
      private readonly List<TreeViewItemViewModel> _expandedNodes = new List<TreeViewItemViewModel>();
      private readonly List<TreeViewItemViewModel> _selectedNodes = new List<TreeViewItemViewModel>();

      public List<TreeViewItemViewModel> ExpandedNodes {
        get { return _expandedNodes; }
      }

      public List<TreeViewItemViewModel> SelectedNodes {
        get { return _selectedNodes; }
      }

      public void ProcessNodes(TreeViewItemViewModel x) {
        if (x.IsExpanded) {
          _expandedNodes.Add(x);
        }
        if (x.IsSelected) {
          SelectedNodes.Add(x);
        }
        x.Children.ForAll(ProcessNodes);
      }
    }

    /// <summary>
    /// Return the top level entry parent of <paramref name="item"/>
    /// </summary>
    private static DirectoryEntryViewModel GetChromiumRoot(TreeViewItemViewModel item) {
      for (TreeViewItemViewModel current = item; current != null; current = current.ParentViewModel) {
        if (current.ParentViewModel is RootTreeViewItemViewModel) {
          // Maybe "null" if top level node is not a directory.
          return current as DirectoryEntryViewModel;
        }
      }
      return null;
    }

    /// <summary>
    /// Navigate to the FileSystemTree directory entry corresponding to
    /// <paramref name="relativePathEntry"/>. This is a no-op if the FileSystemTree
    /// is already the currently active ViewModel.
    /// </summary>
    public void ShowInSourceExplorer(FileSystemEntryViewModel relativePathEntry) {
      // If the view model is displaying the file system tree, don't do anything.
      if (ViewModel.ActiveDisplay == SourceExplorerViewModel.DisplayKind.FileSystemTree)
        return;

      var entry = FindFileSystemEntryForRelativePath(ViewModel.FileSystemTreeNodes, relativePathEntry);
      if (entry != null) {
        // Ensure no node is selected before displaying file system tree.
        ViewModel.FileSystemTreeNodes.ForAll(RemoveSelection);

        // Switch to file system tree ViewModel and select the entry we found.
        ViewModel.SwitchToFileSystemTree();
        BringItemViewModelToView(entry);
      }
    }

    /// <summary>
    /// Navigate to the FileSystemTree directory entry corresponding to
    /// <paramref name="path"/>. This is a no-op if the FileSystemTree
    /// is already the currently active ViewModel.
    /// </summary>
    public void ShowInSourceExplorer(string path) {
      // If the view model is displaying the file system tree, don't do anything.
      //if (ViewModel.ActiveDisplay == SourceExplorerViewModel.DisplayKind.FileSystemTree)
      //  return;

      var entry = FindFileSystemEntryForPath(ViewModel.FileSystemTreeNodes, path);
      if (entry != null) {
        // Ensure no node is selected before displaying file system tree.
        ViewModel.FileSystemTreeNodes.ForAll(RemoveSelection);

        // Switch to file system tree ViewModel and select the entry we found.
        ViewModel.SwitchToFileSystemTree();
        BringItemViewModelToView(entry);
      }
    }

    private void RemoveSelection(TreeViewItemViewModel item) {
      item.IsSelected = false;
      item.Children.ForAll(RemoveSelection);
    }

    public void BringItemViewModelToView(TreeViewItemViewModel item) {
      // We look for the tree view item corresponding to "item", swallowing
      // the "BringIntoView" request to avoid flickering as we descend into
      // the virtual tree and realize the sub-panels at each level.
      _control.SwallowsRequestBringIntoView(true);
      var treeViewItem = SelectTreeViewItem(_control.FileTreeView, item);

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

    public TreeViewItem SelectTreeViewItem(TreeView treeView, TreeViewItemViewModel item) {
      return WpfUtilities.SelectTreeViewItem(treeView, item);
    }

    public bool ExecuteOpenCommandForItem(TreeViewItemViewModel tvi) {
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
      // Cancel all previously running tasks
      _taskCancellation.CancelAll();
      var cancellationToken = _taskCancellation.GetNewToken();

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
          if (cancellationToken.IsCancellationRequested)
            return;
          workerParams.ProcessResponse(typedResponse, sw);
        },
        OnError = errorResponse => {
          if (cancellationToken.IsCancellationRequested)
            return;
          ViewModel.SetErrorResponse(errorResponse);
        }
      };

      _uiRequestProcessor.Post(request);
    }

    public void SetFileSystemTree(FileSystemTree tree) {
      var viewModel = CreateFileSystemTreeViewModel(tree);

      // Transfer expanded and selected nodes from the old tree to the new one.
      TransferFileSystemTreeState(ViewModel.FileSystemTreeNodes, viewModel);

      // Set tree as the new active tree.
      ViewModel.SetFileSystemTree(viewModel);
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
          var viewModel = CreateFileNamesSearchResult(response.SearchResult, msg, true);
          ViewModel.SetFileNamesSearchResult(viewModel);
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
          var viewModel = CreateDirectoryNamesSearchResult(response.SearchResult, msg, true);
          ViewModel.SetDirectoryNamesSearchResult(viewModel);
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
          var viewModel = CreateTextSearchResultViewModel(response.SearchResults, msg, expandAll);
          ViewModel.SetTextSearchResult(viewModel);
        }
      });
    }
  }
}