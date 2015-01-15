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
using VsChromium.Core.Linq;
using VsChromium.Threads;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class FileEntryViewModel : FileSystemEntryViewModel {
    private readonly FileEntry _fileEntry;
    private readonly Lazy<IList<TreeViewItemViewModel>> _children;
    private bool _hasExpanded;

    public FileEntryViewModel(ISourceExplorerViewModelHost host, TreeViewItemViewModel parentViewModel, FileEntry fileEntry)
      : base(host, parentViewModel, fileEntry.Data != null) {
      _fileEntry = fileEntry;
      _children = new Lazy<IList<TreeViewItemViewModel>>(CreateChildren);
    }

    private IList<TreeViewItemViewModel> CreateChildren() {
      return FileSystemEntryDataViewModelFactory.CreateViewModels(Host, this, _fileEntry.Data).ToList();
    }

    public override FileSystemEntry FileSystemEntry { get { return _fileEntry; } }

    public override int ChildrenCount { get { return GetChildren().Count(); } }

    public string Path { get { return GetFullPath(); } }

    public override string DisplayText
    {
      get
      {
        if (ChildrenCount > 0) {
          return string.Format("{0} ({1})", base.DisplayText, ChildrenCount);
        } else {
          return base.DisplayText;
        }
      }
    }

    public override ImageSource ImageSourcePath {
      get {
        return StandarImageSourceFactory.GetImageForDocument(_fileEntry.Name);
      }
    }

    #region Command Handlers

    public ICommand OpenCommand {
      get {
        return CommandDelegate.Create(sender => Host.NavigateToFile(this, null));
      }
    }

    public ICommand CopyFullPathCommand {
      get {
        return CommandDelegate.Create(sender => Host.Clipboard.SetText(GetFullPath()));
      }
    }

    public ICommand CopyRelativePathCommand {
      get {
        return CommandDelegate.Create(sender => Host.Clipboard.SetText(GetRelativePath()));
      }
    }

    public ICommand CopyFullPathPosixCommand {
      get {
        return CommandDelegate.Create(sender => Host.Clipboard.SetText(PathHelpers.ToPosix(GetFullPath())));
      }
    }

    public ICommand CopyRelativePathPosixCommand {
      get {
        return CommandDelegate.Create(sender => Host.Clipboard.SetText(PathHelpers.ToPosix(GetRelativePath())));
      }
    }

    public ICommand OpenContainingFolderCommand {
      get {
        return CommandDelegate.Create(sender => Host.WindowsExplorer.OpenContainingFolder(this.GetFullPath()));
      }
    }

    #endregion

    protected override void OnPropertyChanged(string propertyName) {
      if (propertyName == "IsExpanded") {
        if (IsExpanded && !_hasExpanded) {
          _hasExpanded = true;
          LoadFileExtracts();
        }
      }
      base.OnPropertyChanged(propertyName);
    }

    private void LoadFileExtracts() {
      var positions = GetChildren()
        .OfType<FilePositionViewModel>()
        .ToList();
      if (!positions.Any())
        return;

      var request = new GetFileExtractsRequest {
        FileName = Path,
        Positions = positions
          .Select(x => new FilePositionSpan { Position = x.Position, Length = x.Length })
          .ToList()
      };

      var uiRequest = new UIRequest() {
        Request = request,
        Id = "FileEntryViewModel-" + Path,
        Delay = TimeSpan.FromSeconds(0.0),
        OnSuccess = (typedResponse) => {
          var response = (GetFileExtractsResponse)typedResponse;
          positions
            .Zip(response.FileExtracts, (x, y) => new { FilePositionViewModel = x, FileExtract = y })
            .Where(x => x.FileExtract != null)
            .ForAll(x => x.FilePositionViewModel.SetTextExtract(x.FileExtract));
        }
      };

      this.Host.UIRequestProcessor.Post(uiRequest);
    }

    protected override IEnumerable<TreeViewItemViewModel> GetChildren() {
      return _children.Value;
    }
  }
}
