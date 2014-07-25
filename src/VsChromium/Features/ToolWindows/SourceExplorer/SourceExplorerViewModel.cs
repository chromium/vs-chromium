// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Features.AutoUpdate;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class SourceExplorerViewModel : ChromiumExplorerViewModelBase {
    private List<TreeViewItemViewModel> _directoryNameSearchResultNodes = new List<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _textSearchResultNodes = new List<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _fileNameSearchResultNodes = new List<TreeViewItemViewModel>();
    private List<TreeViewItemViewModel> _fileSystemNodes = new List<TreeViewItemViewModel>();
    private ISourceExplorerViewModelHost _sourceExplorerViewModelHost;
    private UpdateInfo _updateInfo;

    public enum DisplayKind {
      FileSystem,
      FileNameSearchResult,
      DirectoryNameSearchResult,
      TextSearchResult,
    }

    public DisplayKind ActiveDisplay {
      get {
        if (ReferenceEquals(CurrentRootNodesViewModel, _textSearchResultNodes))
          return DisplayKind.TextSearchResult;
        if (ReferenceEquals(CurrentRootNodesViewModel, _fileNameSearchResultNodes))
          return DisplayKind.FileNameSearchResult;
        if (ReferenceEquals(CurrentRootNodesViewModel, _directoryNameSearchResultNodes))
          return DisplayKind.DirectoryNameSearchResult;
        return DisplayKind.FileSystem;
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool MatchCase { get; set; }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool EnableChildDebugging { get; set; }

    public ImageSource LightningBoltImage {
      get {
        return _sourceExplorerViewModelHost.StandarImageSourceFactory.LightningBolt;
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string UpdateInfoText {
      get {
        if (_updateInfo == null)
          return "";
        return string.Format("A new version ({0}) of VsChromium is available: ", _updateInfo.Version);
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public string UpdateInfoUrl {
      get {
        if (_updateInfo == null)
          return "";
        return _updateInfo.Url.ToString();
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public Visibility UpdateInfoVisibility {
      get {
        if (_updateInfo == null)
          return Visibility.Collapsed;
        return Visibility.Visible;
      }
    }

    public UpdateInfo UpdateInfo {
      get {
        return _updateInfo;
      }
      set {
        _updateInfo = value;
        OnPropertyChanged("UpdateInfoText");
        OnPropertyChanged("UpdateInfoUrl");
        OnPropertyChanged("UpdateInfoVisibility");
      }
    }

    public void SetHost(ISourceExplorerViewModelHost sourceExplorerViewModelHost) {
      _sourceExplorerViewModelHost = sourceExplorerViewModelHost;
    }

    public ISourceExplorerViewModelHost Host { get { return _sourceExplorerViewModelHost; }}

    public void SwitchToFileSystemTree() {
      SetRootNodes(_fileSystemNodes, "Open a source file from a local Chromium enlistment.");
    }

    private void SwitchToFileNamesSearchResult() {
      SetRootNodes(_fileNameSearchResultNodes);
    }

    private void SwitchToDirectoryNamesSearchResult() {
      SetRootNodes(_directoryNameSearchResultNodes);
    }

    private void SwitchToFileContentsSearchResult() {
      SetRootNodes(_textSearchResultNodes);
    }

    public void SetFileSystemTree(FileSystemTree tree) {
      var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
      _fileSystemNodes = new List<TreeViewItemViewModel>(tree.Root
        .Entries
        .Select(x => FileSystemEntryViewModel.Create(_sourceExplorerViewModelHost, rootNode, x)));
      _fileSystemNodes.ForAll(x => rootNode.AddChild(x));
      ExpandNodes(_fileSystemNodes, false);
      SwitchToFileSystemTree();
    }

    public void SetFileNamesSearchResult(DirectoryEntry fileResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
      _fileNameSearchResultNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(_sourceExplorerViewModelHost.StandarImageSourceFactory, rootNode, description)
        }.Concat(
          fileResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_sourceExplorerViewModelHost, rootNode, x)))
          .ToList();
      _fileNameSearchResultNodes.ForAll(x => rootNode.AddChild(x));
      ExpandNodes(_fileNameSearchResultNodes, expandAll);
      SwitchToFileNamesSearchResult();
    }

    public void SetDirectoryNamesSearchResult(DirectoryEntry directoryResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
      _directoryNameSearchResultNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(ImageSourceFactory, rootNode, description)
        }.Concat(
          directoryResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_sourceExplorerViewModelHost, rootNode, x)))
          .ToList();
      _directoryNameSearchResultNodes.ForAll(x => rootNode.AddChild(x));
      ExpandNodes(_directoryNameSearchResultNodes, expandAll);
      SwitchToDirectoryNamesSearchResult();
    }

    public void SetFileContentsSearchResult(DirectoryEntry searchResults, string description, bool expandAll) {
      var rootNode = new RootTreeViewItemViewModel(ImageSourceFactory);
      _textSearchResultNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(ImageSourceFactory, rootNode, description)
        }.Concat(
          searchResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_sourceExplorerViewModelHost, rootNode, x)))
          .ToList();
      _textSearchResultNodes.ForAll(x => rootNode.AddChild(x));
      ExpandNodes(_textSearchResultNodes, expandAll);
      SwitchToFileContentsSearchResult();
    }

    public void SelectDirectory(DirectoryEntryViewModel directoryEntry, TreeView treeView, Action beforeSelectItem, Action afterSelectItem) {
      if (ReferenceEquals(CurrentRootNodesViewModel, _fileSystemNodes))
        return;

      var chromiumRoot = GetChromiumRoot(directoryEntry);
      Debug.Assert(chromiumRoot != null);

      var entryViewModel =
        _fileSystemNodes.OfType<DirectoryEntryViewModel>()
          .FirstOrDefault(x => SystemPathComparer.Instance.Comparer.Equals(x.Name, chromiumRoot.Name));
      if (entryViewModel == null)
        return;

      foreach (var childName in directoryEntry.Name.Split(Path.DirectorySeparatorChar)) {
        var childViewModel = entryViewModel
          .Children
          .OfType<DirectoryEntryViewModel>()
          .FirstOrDefault(x => SystemPathComparer.Instance.Comparer.Equals(x.Name, childName));
        if (childViewModel == null) {
          entryViewModel.EnsureAllChildrenLoaded();
          childViewModel = entryViewModel
            .Children
            .OfType<DirectoryEntryViewModel>()
            .FirstOrDefault(x => SystemPathComparer.Instance.Comparer.Equals(x.Name, childName));
          if (childViewModel == null)
            return;
        }

        entryViewModel = childViewModel;
      }

      SwitchToFileSystemTree();
      SelectItem(entryViewModel, treeView, beforeSelectItem, afterSelectItem);
    }

    public void SelectItem(TreeViewItemViewModel item, TreeView treeView, Action beforeSelectItem, Action afterSelectItem) {
      item.IsExpanded = true;
      item.IsSelected = true;
      WpfUtilities.Invoke(treeView, DispatcherPriority.ApplicationIdle, () => {
        try {
          beforeSelectItem();
          try {
            WpfUtilities.SelectItem(treeView, item);
          }
          finally {
            afterSelectItem();
          }
        }
        catch (Exception e) {
          Logger.LogException(e, "Error updating TreeView UI to show selected item.");
        }
      });
    }

    private DirectoryEntryViewModel GetChromiumRoot(DirectoryEntryViewModel directoryEntry) {
      for (TreeViewItemViewModel current = directoryEntry; current != null; current = current.ParentViewModel) {
        if (current.ParentViewModel is RootTreeViewItemViewModel) {
          var model = current as DirectoryEntryViewModel;
          if (model != null)
            return model;
          break;
        }
      }

      return null;
    }

    public void FileSystemTreeComputing() {
      if (!_fileSystemNodes.Any()) {
        SetRootNodes(_fileSystemNodes, "(Loading files from Chromium enlistment...)");
      }
    }

    public void SetErrorResponse(ErrorResponse errorResponse) {
      var messages = new List<TreeViewItemViewModel>();
      var rootError = new RootErrorItemViewModel(ImageSourceFactory, null, "Error processing request. You may need to restart Visual Studio.");
      messages.Add(rootError);
      while (errorResponse != null) {
        rootError.Children.Add(new TextItemViewModel(ImageSourceFactory, rootError, errorResponse.Message));
        errorResponse = errorResponse.InnerError;
      }
      SetRootNodes(messages);
    }
  }
}
