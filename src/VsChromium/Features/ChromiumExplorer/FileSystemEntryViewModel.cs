// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.ChromiumExplorer {
  public abstract class FileSystemEntryViewModel : TreeViewItemViewModel {
    protected FileSystemEntryViewModel(
      ITreeViewItemViewModelHost host,
      TreeViewItemViewModel parentViewModel,
      bool lazyLoadChildren)
      : base(host, parentViewModel, lazyLoadChildren) {
    }

    public abstract FileSystemEntry FileSystemEntry { get; }

    public string Name { get { return FileSystemEntry.Name; } }

    public string AdditionalText { get; set; }

    public string DisplayText {
      get {
        var result = Name;
        if (!string.IsNullOrEmpty(AdditionalText)) {
          result += AdditionalText;
        }
        return result;
      }
    }

    public static FileSystemEntryViewModel Create(
      ITreeViewItemViewModelHost host,
      TreeViewItemViewModel parentViewModel,
      FileSystemEntry fileSystemEntry) {
      var fileEntry = fileSystemEntry as FileEntry;
      if (fileEntry != null)
        return new FileEntryViewModel(host, parentViewModel, fileEntry);
      else
        return new DirectoryEntryViewModel(host, parentViewModel, (DirectoryEntry)fileSystemEntry);
    }

    public string GetPath() {
      var parent = ParentViewModel as FileSystemEntryViewModel;
      if (parent == null)
        return Name;
      return Path.Combine(parent.GetPath(), Name);
    }
  }
}
