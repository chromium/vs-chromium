// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using VsChromium.Core;
using VsChromium.Core.FileNames;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Features.AutoUpdate;
using VsChromium.Threads;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class SourceExplorerViewModel : INotifyPropertyChanged {
    private readonly TreeViewRootNodes _rootNodes = new TreeViewRootNodes();
    private IComponentModel _componentModel;
    private IList<TreeViewItemViewModel> _currentRootNodesViewModel;
    private IEnumerable<TreeViewItemViewModel> _directoryPathRootNodes = new List<TreeViewItemViewModel>();
    private IEnumerable<TreeViewItemViewModel> _fileContentsResultRootNodes = new List<TreeViewItemViewModel>();
    private IEnumerable<TreeViewItemViewModel> _fileNamesResultRootNodes = new List<TreeViewItemViewModel>();
    private IEnumerable<TreeViewItemViewModel> _fileSystemEntryRootNodes = new List<TreeViewItemViewModel>();
    private ITreeViewItemViewModelHost _host;
    private UpdateInfo _updateInfo;

    /// <summary>
    /// Databound!
    /// </summary>
    public TreeViewRootNodes RootNodes { get { return _rootNodes; } }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool MatchCase { get; set; }

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

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnToolWindowCreated(IServiceProvider serviceProvider) {
      _componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
      var standarImageSourceFactory = _componentModel.DefaultExportProvider.GetExportedValue<IStandarImageSourceFactory>();
      var uiRequestProcessor = _componentModel.DefaultExportProvider.GetExportedValue<IUIRequestProcessor>();
      _host = new TreeViewItemViewModelHost(standarImageSourceFactory, uiRequestProcessor);
    }

    public void SwitchToFileSystemTree() {
      SetRootNodes(_fileSystemEntryRootNodes, "Open a source file from a local Chromium enlistment.");
    }

    private void SwitchToFileNamesSearchResult() {
      SetRootNodes(_fileNamesResultRootNodes);
    }

    private void SwitchToDirectoryNamesSearchResult() {
      SetRootNodes(_directoryPathRootNodes);
    }

    private void SwitchToFileContentsSearchResult() {
      SetRootNodes(_fileContentsResultRootNodes);
    }

    public void SetFileSystemTree(FileSystemTree tree) {
      _fileSystemEntryRootNodes = tree.Root
        .Entries
        .Select(x => FileSystemEntryViewModel.Create(_host, null, x))
        .ToList();
      ExpandNodes(_fileSystemEntryRootNodes, false);
      SwitchToFileSystemTree();
    }

    public void SetFileNamesSearchResult(DirectoryEntry fileResults, string description, bool expandAll) {
      _fileNamesResultRootNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(_host, null, description)
        }.Concat(
          fileResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_host, null, x)))
          .ToList();
      ExpandNodes(_fileNamesResultRootNodes, expandAll);
      SwitchToFileNamesSearchResult();
    }

    public void SetDirectoryNamesSearchResult(DirectoryEntry directoryResults, string description, bool expandAll) {
      _directoryPathRootNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(_host, null, description)
        }.Concat(
          directoryResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_host, null, x)))
          .ToList();
      ExpandNodes(_directoryPathRootNodes, expandAll);
      SwitchToDirectoryNamesSearchResult();
    }

    public void SetFileContentsSearchResult(DirectoryEntry searchResults, string description, bool expandAll) {
      _fileContentsResultRootNodes =
        new List<TreeViewItemViewModel> {
          new TextItemViewModel(_host, null, description)
        }.Concat(
          searchResults
            .Entries
            .Select(x => FileSystemEntryViewModel.Create(_host, null, x)))
          .ToList();
      ExpandNodes(_fileContentsResultRootNodes, expandAll);
      SwitchToFileContentsSearchResult();
    }

    private void SetRootNodes(IEnumerable<TreeViewItemViewModel> source, string defaultText = "") {
      _currentRootNodesViewModel = source.ToList();
      if (_currentRootNodesViewModel.Count == 0 && !string.IsNullOrEmpty(defaultText)) {
        _currentRootNodesViewModel.Add(new TextItemViewModel(_host, null, defaultText));
      }
      _rootNodes.Clear();
      _currentRootNodesViewModel.ForAll(x => _rootNodes.Add(x));
    }

    private void ExpandNodes(IEnumerable<TreeViewItemViewModel> source, bool expandAll) {
      source.ForAll(x => {
        if (expandAll)
          ExpandAll(x);
        else
          x.IsExpanded = true;
      });
    }

    private void ExpandAll(TreeViewItemViewModel item) {
      item.IsExpanded = true;
      item.Children.ForAll(x => ExpandAll(x));
    }

    protected virtual void OnPropertyChanged(string propertyName) {
      PropertyChangedEventHandler handler = PropertyChanged;
      if (handler != null)
        handler(this, new PropertyChangedEventArgs(propertyName));
    }

    public void SelectDirectory(DirectoryEntryViewModel directoryEntry, TreeView treeView, Action beforeSelectItem, Action afterSelectItem) {
      if (ReferenceEquals(_currentRootNodesViewModel, _fileSystemEntryRootNodes))
        return;

      var chromiumRoot = GetChromiumRoot(directoryEntry);

      var entryViewModel =
        _fileSystemEntryRootNodes.OfType<DirectoryEntryViewModel>()
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
      entryViewModel.IsExpanded = true;
      entryViewModel.IsSelected = true;
      WpfUtilities.Invoke(treeView, DispatcherPriority.ApplicationIdle, () => {
        try {
          beforeSelectItem();
          try {
            WpfUtilities.SelectItem(treeView, entryViewModel);
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
      for (TreeViewItemViewModel parent = directoryEntry; parent != null; parent = parent.ParentViewModel) {
        if (parent.ParentViewModel == null) {
          var model = parent as DirectoryEntryViewModel;
          if (model != null)
            return model;
          break;
        }
      }

      return null;
    }

    public void FileSystemTreeComputing() {
      if (!_fileSystemEntryRootNodes.Any()) {
        SetRootNodes(_fileSystemEntryRootNodes, "(Loading files from Chromium enlistment...)");
      }
    }

    public void SetErrorResponse(ErrorResponse errorResponse) {
      var messages = new List<TreeViewItemViewModel>();
      var rootError = new RootErrorItemViewModel(_host, null, "Error processing request. You may need to restart Visual Studio.");
      messages.Add(rootError);
      while (errorResponse != null) {
        rootError.Children.Add(new TextItemViewModel(_host, rootError, errorResponse.Message));
        errorResponse = errorResponse.InnerError;
      }
      SetRootNodes(messages);
    }
  }
}
