// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Threads;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class SourceExplorerViewModelHost : ISourceExplorerViewModelHost {
    private readonly SourceExplorerControl _control;
    private readonly IUIRequestProcessor _uiRequestProcessor;
    private readonly IStandarImageSourceFactory _standarImageSourceFactory;
    private readonly IWindowsExplorer _windowsExplorer;
    private readonly IClipboard _clipboard;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly IOpenDocumentHelper _openDocumentHelper;

    public SourceExplorerViewModelHost(
      SourceExplorerControl control,
      IUIRequestProcessor uiRequestProcessor,
      IStandarImageSourceFactory standarImageSourceFactory,
      IWindowsExplorer windowsExplorer,
      IClipboard clipboard,
      ISynchronizationContextProvider synchronizationContextProvider,
      IOpenDocumentHelper openDocumentHelper) {
      _control = control;
      _uiRequestProcessor = uiRequestProcessor;
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
  }
}