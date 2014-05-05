// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.FileNames;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public abstract class FileSystemEntryViewModel : SourceExplorerItemViewModelBase {
    protected FileSystemEntryViewModel(
        ISourceExplorerViewModelHost host,
        TreeViewItemViewModel parentViewModel,
        bool lazyLoadChildren)
      : base(host, parentViewModel, lazyLoadChildren) {
    }

    public abstract FileSystemEntry FileSystemEntry { get; }

    public string Name { get { return FileSystemEntry.Name; } }

    public virtual string DisplayText {
      get {
        return this.Name;
      }
    }

    public static FileSystemEntryViewModel Create(ISourceExplorerViewModelHost host, TreeViewItemViewModel parentViewModel, FileSystemEntry fileSystemEntry) {
      var fileEntry = fileSystemEntry as FileEntry;
      if (fileEntry != null)
        return new FileEntryViewModel(host, parentViewModel, fileEntry);
      else
        return new DirectoryEntryViewModel(host, parentViewModel, (DirectoryEntry)fileSystemEntry);
    }

    public string GetFullPath() {
      var parent = ParentViewModel as FileSystemEntryViewModel;
      if (parent == null)
        return Name;
      return PathHelpers.PathCombine(parent.GetFullPath(), Name);
    }

    public string GetRelativePath() {
      var parent = ParentViewModel as FileSystemEntryViewModel;
      if (parent == null)
        return "";
      return PathHelpers.PathCombine(parent.GetRelativePath(), Name);
    }
  }
}
