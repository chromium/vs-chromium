// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  public class DirectoryEntryViewModel : FileSystemEntryViewModel {
    private readonly DirectoryEntry _directoryEntry;
    private readonly Lazy<IList<TreeViewItemViewModel>> _children;

    public DirectoryEntryViewModel(
        ITreeViewItemViewModelHost host,
        TreeViewItemViewModel parentViewModel,
        DirectoryEntry directoryEntry)
        : base(host, parentViewModel, directoryEntry.Entries.Count > 0) {
      this._directoryEntry = directoryEntry;
      this._children = new Lazy<IList<TreeViewItemViewModel>>(CreateChildren);
    }

    private IList<TreeViewItemViewModel> CreateChildren() {
      return this._directoryEntry.Entries
        .Select(x => (TreeViewItemViewModel)FileSystemEntryViewModel.Create(this.Host, this, x))
        .ToList();
    }

    public override FileSystemEntry FileSystemEntry {
      get {
        return this._directoryEntry;
      }
    }

    public override int ChildrenCount {
      get {
        return this._directoryEntry.Entries.Count;
      }
    }

    public override ImageSource ImageSourcePath {
      get {
        return IsExpanded ? StandarImageSourceFactory.OpenFolder : StandarImageSourceFactory.ClosedFolder;
      }
    }

    protected override IEnumerable<TreeViewItemViewModel> GetChildren() {
      return this._children.Value;
    }

    protected override void OnPropertyChanged(string propertyName) {
      base.OnPropertyChanged(propertyName);
      if (propertyName == "IsExpanded") {
        OnPropertyChanged("ImageSourcePath");
      }
    }
  }
}
