// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using VsChromiumCore;
using VsChromiumCore.FileNames;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Linq;
using VsChromiumPackage.Threads;
using VsChromiumPackage.Views;
using VsChromiumPackage.Wpf;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  public class FileExplorerViewModel : INotifyPropertyChanged {
    private readonly FileExplorerRootNodes _rootNodes = new FileExplorerRootNodes();
    private IComponentModel _componentModel;
    private IList<TreeViewItemViewModel> _currentRootNodesViewModel;
    private IEnumerable<TreeViewItemViewModel> _directoryPathRootNodes = new List<TreeViewItemViewModel>();
    private IEnumerable<TreeViewItemViewModel> _fileContentsResultRootNodes = new List<TreeViewItemViewModel>();
    private IEnumerable<TreeViewItemViewModel> _fileNamesResultRootNodes = new List<TreeViewItemViewModel>();
    private IEnumerable<TreeViewItemViewModel> _fileSystemEntryRootNodes = new List<TreeViewItemViewModel>();
    private ITreeViewItemViewModelHost _host;

    /// <summary>
    /// Databound!
    /// </summary>
    public FileExplorerRootNodes RootNodes {
      get {
        return this._rootNodes;
      }
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool MatchCase { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnToolWindowCreated(IServiceProvider serviceProvider) {
      this._componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
      var standarImageSourceFactory = this._componentModel.DefaultExportProvider.GetExportedValue<IStandarImageSourceFactory>();
      var uiRequestProcessor = this._componentModel.DefaultExportProvider.GetExportedValue<IUIRequestProcessor>();
      this._host = new TreeViewItemViewModelHost(standarImageSourceFactory, uiRequestProcessor);
    }

    public void SwitchToFileSystemTree() {
      SetRootNodes(this._fileSystemEntryRootNodes, "Open a source file from a local Chromium enlistment.");
    }

    private void SwitchToFileNamesSearchResult() {
      SetRootNodes(this._fileNamesResultRootNodes);
    }

    private void SwitchToDirectoryNamesSearchResult() {
      SetRootNodes(this._directoryPathRootNodes);
    }

    private void SwitchToFileContentsSearchResult() {
      SetRootNodes(this._fileContentsResultRootNodes);
    }

    public void SetFileSystemTree(FileSystemTree tree) {
      this._fileSystemEntryRootNodes = tree.Root
          .Entries
          .Select(x => FileSystemEntryViewModel.Create(this._host, null, x))
          .ToList();
      ExpandNodes(this._fileSystemEntryRootNodes, false);
      SwitchToFileSystemTree();
    }

    public void SetFileNamesSearchResult(DirectoryEntry fileResults, string description, bool expandAll) {
      this._fileNamesResultRootNodes =
          new List<TreeViewItemViewModel> {
            new TextItemViewModel(this._host, null, description)
          }.Concat(
              fileResults
                  .Entries
                  .Select(x => FileSystemEntryViewModel.Create(this._host, null, x)))
              .ToList();
      ExpandNodes(this._fileNamesResultRootNodes, expandAll);
      SwitchToFileNamesSearchResult();
    }

    public void SetDirectoryNamesSearchResult(DirectoryEntry directoryResults, string description, bool expandAll) {
      this._directoryPathRootNodes =
          new List<TreeViewItemViewModel> {
            new TextItemViewModel(this._host, null, description)
          }.Concat(
              directoryResults
                  .Entries
                  .Select(x => FileSystemEntryViewModel.Create(this._host, null, x)))
              .ToList();
      ExpandNodes(this._directoryPathRootNodes, expandAll);
      SwitchToDirectoryNamesSearchResult();
    }

    public void SetFileContentsSearchResult(DirectoryEntry searchResults, string description, bool expandAll) {
      this._fileContentsResultRootNodes =
          new List<TreeViewItemViewModel> {
            new TextItemViewModel(this._host, null, description)
          }.Concat(
              searchResults
                  .Entries
                  .Select(x => FileSystemEntryViewModel.Create(this._host, null, x)))
              .ToList();
      ExpandNodes(this._fileContentsResultRootNodes, expandAll);
      SwitchToFileContentsSearchResult();
    }

    private void SetRootNodes(IEnumerable<TreeViewItemViewModel> source, string defaultText = "") {
      this._currentRootNodesViewModel = source.ToList();
      if (this._currentRootNodesViewModel.Count == 0 && !string.IsNullOrEmpty(defaultText)) {
        this._currentRootNodesViewModel.Add(new TextItemViewModel(this._host, null, defaultText));
      }
      this._rootNodes.Clear();
      this._currentRootNodesViewModel.ForAll(x => this._rootNodes.Add(x));
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
      if (ReferenceEquals(this._currentRootNodesViewModel, this._fileSystemEntryRootNodes))
        return;

      var chromiumRoot = GetChromiumRoot(directoryEntry);

      var entryViewModel =
          this._fileSystemEntryRootNodes.OfType<DirectoryEntryViewModel>()
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
      if (!this._fileSystemEntryRootNodes.Any()) {
        SetRootNodes(this._fileSystemEntryRootNodes, "(Loading files from Chromium enlistment...)");
      }
    }
  }
}
