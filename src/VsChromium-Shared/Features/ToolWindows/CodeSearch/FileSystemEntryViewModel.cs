// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public abstract class FileSystemEntryViewModel : CodeSearchItemViewModelBase {
    protected FileSystemEntryViewModel(
        ICodeSearchController controller,
        TreeViewItemViewModel parentViewModel,
        bool lazyLoadChildren)
      : base(controller, parentViewModel, lazyLoadChildren) {
    }

    public abstract FileSystemEntry FileSystemEntry { get; }

    public string Name { get { return FileSystemEntry.Name; } }

    public override string DisplayText {
      get {
        return this.Name;
      }
    }

    public static FileSystemEntryViewModel Create(
      ICodeSearchController host,
      TreeViewItemViewModel parentViewModel,
      FileSystemEntry fileSystemEntry, Action<FileSystemEntryViewModel> postCreate) {
      var fileEntry = fileSystemEntry as FileEntry;
      if (fileEntry != null) {
        var result = new FileEntryViewModel(host, parentViewModel, fileEntry);
        postCreate(result);
        return result;
      }
      else {
        var result = new DirectoryEntryViewModel(host, parentViewModel, (DirectoryEntry) fileSystemEntry, postCreate);
        postCreate(result);
        return result;
      }
    }

    public string GetFullPath() {
      var parent = ParentViewModel as FileSystemEntryViewModel;
      if (parent == null)
        return Name;
      return PathHelpers.CombinePaths(parent.GetFullPath(), Name);
    }

    public string GetRelativePath() {
      var parent = ParentViewModel as FileSystemEntryViewModel;
      if (parent == null)
        return "";
      return PathHelpers.CombinePaths(parent.GetRelativePath(), Name);
    }
  }
}
