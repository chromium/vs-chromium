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
using VsChromium.Core.Utility;
using VsChromium.Threads;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class FileEntryViewModel : FileSystemEntryViewModel {
    private readonly FileEntry _fileEntry;
    private readonly Lazy<IList<TreeViewItemViewModel>> _children;
    private int _lineNumber = -1;
    private int _columnNumber = -1;
    private bool _hasExpanded;

    public FileEntryViewModel(ICodeSearchController controller, TreeViewItemViewModel parentViewModel, FileEntry fileEntry)
      : base(controller, parentViewModel, fileEntry.Data != null) {
      _fileEntry = fileEntry;
      _children = new Lazy<IList<TreeViewItemViewModel>>(CreateChildren);
    }

    private IList<TreeViewItemViewModel> CreateChildren() {
      return FileSystemEntryDataViewModelFactory.CreateViewModels(Controller, this, _fileEntry.Data).ToList();
    }

    public override FileSystemEntry FileSystemEntry { get { return _fileEntry; } }

    public override int ChildrenCount { get { return GetChildren().Count(); } }

    public string Path { get { return GetFullPath(); } }

    /// <summary>
    /// Databound! Return text representing  of items (if children are present)
    /// </summary>
    public string LineColumnText {
      get {
        if (_lineNumber < 0)
          return "";
        
        if (_columnNumber < 0)
          return string.Format("({0}) ", _lineNumber + 1);

        return string.Format("({0},{1}) ", _lineNumber + 1, _columnNumber + 1);
      }
    }

    /// <summary>
    /// Databound! Return text representing  of items (if children are present)
    /// </summary>
    public string ItemCountText {
      get {
        if (ChildrenCount == 0)
          return "";

        if (ChildrenCount == 1)
          return " (1 item)";

        return string.Format(" ({0} items)", ChildrenCount);
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
        return CommandDelegate.Create(sender => Controller.OpenFileInEditor(this, _lineNumber, _columnNumber, 0));
      }
    }

    public ICommand OpenWithCommand {
      get {
        return CommandDelegate.Create(sender => Controller.OpenFileInEditorWith(this, _lineNumber, _columnNumber, 0));
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

    public ICommand ShowInSourceExplorerCommand {
      get {
        return CommandDelegate.Create(
          sender => Controller.ShowInSourceExplorer(this),
          sender => Controller.GlobalSettings.EnableSourceExplorerHierarchy);
      }
    }

    #endregion

    protected override void OnPropertyChanged(string propertyName) {
      if (propertyName == ReflectionUtils.GetPropertyName(this, x => x.IsExpanded)) {
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
        MaxExtractLength = Controller.GlobalSettings.MaxTextExtractLength,
        Positions = positions
          .Select(x => new FilePositionSpan {
            Position = x.Position,
            Length = x.Length
          })
          .ToList()
      };

      var uiRequest = new UIRequest {
        Request = request,
        Id = "FileEntryViewModel-" + Path,
        Delay = TimeSpan.FromSeconds(0.0),
        OnSuccess = (typedResponse) => {
          var response = (GetFileExtractsResponse)typedResponse;
          positions
            .Zip(response.FileExtracts, (x, y) => new {
              FilePositionViewModel = x,
              FileExtract = y
            })
            .Where(x => x.FileExtract != null)
            .ForAll(x => x.FilePositionViewModel.SetTextExtract(x.FileExtract));
        }
      };

      this.Controller.UIRequestProcessor.Post(uiRequest);
    }

    protected override IEnumerable<TreeViewItemViewModel> GetChildren() {
      return _children.Value;
    }

    public void SetLineColumn(int lineNumber, int columnNumber) {
      _lineNumber = lineNumber;
      _columnNumber = columnNumber;
    }
  }
}
