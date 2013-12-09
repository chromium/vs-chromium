// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Linq;
using VsChromiumPackage.Threads;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  public class FileEntryViewModel : FileSystemEntryViewModel {
    private readonly FileEntry _fileEntry;
    private readonly Lazy<IList<TreeViewItemViewModel>> _children;
    private bool _hasExpanded;

    public FileEntryViewModel(
        ITreeViewItemViewModelHost host,
        TreeViewItemViewModel parentViewModel,
        FileEntry fileEntry)
        : base(host, parentViewModel, fileEntry.Data != null) {
      this._fileEntry = fileEntry;
      this._children = new Lazy<IList<TreeViewItemViewModel>>(CreateChildren);
    }

    private IList<TreeViewItemViewModel> CreateChildren() {
      return FileSystemEntryDataViewModelFactory.CreateViewModels(this.Host, this, this._fileEntry.Data).ToList();
    }

    public override FileSystemEntry FileSystemEntry {
      get {
        return this._fileEntry;
      }
    }

    public override int ChildrenCount {
      get {
        return GetChildren().Count();
      }
    }

    public string Path {
      get {
        return GetPath();
      }
    }

    public override ImageSource ImageSourcePath {
      get {
        return StandarImageSourceFactory.GetImageForDocument(this._fileEntry.Name);
      }
    }


    protected override void OnPropertyChanged(string propertyName) {
      if (propertyName == "IsExpanded") {
        if (this.IsExpanded && !this._hasExpanded) {
          this._hasExpanded = true;
          LoadFileExtracts();
        }
      }
    }

    private void LoadFileExtracts() {
      var positions = GetChildren()
        .OfType<FilePositionViewModel>()
        .ToList();
      if (!positions.Any())
        return;

      var request = new GetFileExtractsRequest {
        FileName = Path,
        Positions = positions.Select(x => new FilePositionSpan { Position = x.Position, Length = x.Length }).ToList()
      };

      var uiRequest = new UIRequest() {
        TypedRequest = request,
        Id = "FileEntryViewModel-" + this.Path,
        Delay = TimeSpan.FromSeconds(0.0),
        Callback = (typedResponse) => {
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
      return this._children.Value;
    }
  }
}
