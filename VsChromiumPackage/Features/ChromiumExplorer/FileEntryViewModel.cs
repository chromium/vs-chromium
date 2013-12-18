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

namespace VsChromiumPackage.Features.ChromiumExplorer {
  public class FileEntryViewModel : FileSystemEntryViewModel {
    private readonly FileEntry _fileEntry;
    private readonly Lazy<IList<TreeViewItemViewModel>> _children;
    private bool _hasExpanded;

    public FileEntryViewModel(
      ITreeViewItemViewModelHost host,
      TreeViewItemViewModel parentViewModel,
      FileEntry fileEntry)
      : base(host, parentViewModel, fileEntry.Data != null) {
      _fileEntry = fileEntry;
      _children = new Lazy<IList<TreeViewItemViewModel>>(CreateChildren);
    }

    private IList<TreeViewItemViewModel> CreateChildren() {
      return FileSystemEntryDataViewModelFactory.CreateViewModels(Host, this, _fileEntry.Data).ToList();
    }

    public override FileSystemEntry FileSystemEntry { get { return _fileEntry; } }

    public override int ChildrenCount { get { return GetChildren().Count(); } }

    public string Path { get { return GetPath(); } }

    public override ImageSource ImageSourcePath { get { return StandarImageSourceFactory.GetImageForDocument(_fileEntry.Name); } }

    protected override void OnPropertyChanged(string propertyName) {
      if (propertyName == "IsExpanded") {
        if (IsExpanded && !_hasExpanded) {
          _hasExpanded = true;
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
        Positions = positions.Select(x => new FilePositionSpan {Position = x.Position, Length = x.Length}).ToList()
      };

      var uiRequest = new UIRequest() {
        TypedRequest = request,
        Id = "FileEntryViewModel-" + Path,
        Delay = TimeSpan.FromSeconds(0.0),
        Callback = (typedResponse) => {
          var response = (GetFileExtractsResponse)typedResponse;
          positions
            .Zip(response.FileExtracts, (x, y) => new {FilePositionViewModel = x, FileExtract = y})
            .Where(x => x.FileExtract != null)
            .ForAll(x => x.FilePositionViewModel.SetTextExtract(x.FileExtract));
        }
      };

      Host.UIRequestProcessor.Post(uiRequest);
    }

    protected override IEnumerable<TreeViewItemViewModel> GetChildren() {
      return _children.Value;
    }
  }
}
