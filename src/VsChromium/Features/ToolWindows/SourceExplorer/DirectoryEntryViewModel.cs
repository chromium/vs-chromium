// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class DirectoryEntryViewModel : FileSystemEntryViewModel {
    private readonly DirectoryEntry _directoryEntry;
    private readonly Lazy<IList<TreeViewItemViewModel>> _children;

    public DirectoryEntryViewModel(
        ISourceExplorerController controller,
        TreeViewItemViewModel parentViewModel,
        DirectoryEntry directoryEntry)
      : base(controller, parentViewModel, directoryEntry.Entries.Count > 0) {
      _directoryEntry = directoryEntry;
      _children = new Lazy<IList<TreeViewItemViewModel>>(CreateChildren);
    }

    private IList<TreeViewItemViewModel> CreateChildren() {
      return _directoryEntry.Entries
        .Select(x => (TreeViewItemViewModel)FileSystemEntryViewModel.Create(
            this.Controller,
            this, 
            x))
        .ToList();
    }

    public override FileSystemEntry FileSystemEntry { get { return _directoryEntry; } }

    public override int ChildrenCount { get { return _directoryEntry.Entries.Count; } }

    public override ImageSource ImageSourcePath { get { return IsExpanded ? StandarImageSourceFactory.OpenFolder : StandarImageSourceFactory.ClosedFolder; } }

    protected override IEnumerable<TreeViewItemViewModel> GetChildren() {
      return _children.Value;
    }

    #region Command Handlers

    public ICommand OpenCommand {
      get {
        return CommandDelegate.Create(sender => Controller.ShowInSourceExplorer(this));
      }
    }

    public ICommand CopyFullPathCommand {
      get {
        return CommandDelegate.Create(sender => Controller.Clipboard.SetText(GetFullPath()));
      }
    }

    public ICommand CopyRelativePathCommand {
      get {
        return CommandDelegate.Create(sender => Controller.Clipboard.SetText(GetRelativePath()));
      }
    }

    public ICommand CopyFullPathPosixCommand {
      get {
        return CommandDelegate.Create(sender => Controller.Clipboard.SetText(PathHelpers.ToPosix(GetFullPath())));
      }
    }

    public ICommand CopyRelativePathPosixCommand {
      get {
        return CommandDelegate.Create(sender => Controller.Clipboard.SetText(PathHelpers.ToPosix(GetRelativePath())));
      }
    }

    public ICommand OpenContainingFolderCommand {
      get {
        return CommandDelegate.Create(sender => Controller.WindowsExplorer.OpenContainingFolder(this.GetFullPath()));
      }
    }

    #endregion


    protected override void OnPropertyChanged(string propertyName) {
      base.OnPropertyChanged(propertyName);
      if (propertyName == ReflectionUtils.GetPropertyName(this, x => x.IsExpanded)) {
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.ImageSourcePath));
      }
    }
  }
}
