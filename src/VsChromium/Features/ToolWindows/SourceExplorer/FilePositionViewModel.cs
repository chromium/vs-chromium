// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class FilePositionViewModel : SourceExplorerItemViewModelBase {
    private readonly FileEntryViewModel _parentFile;
    private readonly FilePositionSpan _position;
    private FileExtract _fileExtract;

    public FilePositionViewModel(ISourceExplorerViewModelHost host, FileEntryViewModel parentFile, FilePositionSpan position)
      : base(host, parentFile, false) {
      _parentFile = parentFile;
      _position = position;
    }

    public FileEntryViewModel ParentFile { get { return _parentFile; } }

    public int Position { get { return _position.Position; } }

    public int Length { get { return _position.Length; } }

    public string Path {
      get {
        return ParentFile.Path;
      }
    }

    public string DisplayText {
      get {
        if (_fileExtract != null) {
          return string.Format("{0} ({1}, {2})", _fileExtract.Text.Trim(), _fileExtract.LineNumber + 1, _fileExtract.ColumnNumber + 1);
        } else {
          return string.Format("File offset {0}", Position);
        }
      }
    }

    public override ImageSource ImageSourcePath { get { return StandarImageSourceFactory.GetImage("FileGo"); } }

    public void SetTextExtract(FileExtract value) {
      _fileExtract = value;
      OnPropertyChanged("DisplayText");
    }

    #region Command Handlers

    public ICommand OpenCommand {
      get {
        return CommandDelegate.Create(sender => Host.NavigateToFile(ParentFile, new Span(Position, Length)));
      }
    }

    public ICommand CopyFullPathCommand {
      get {
        return CommandDelegate.Create(sender => Host.Clipboard.SetText(ParentFile.GetFullPath()));
      }
    }

    public ICommand CopyRelativePathCommand {
      get {
        return CommandDelegate.Create(sender => Host.Clipboard.SetText(ParentFile.GetRelativePath()));
      }
    }

    public ICommand CopyFullPathPosixCommand {
      get {
        return CommandDelegate.Create(sender => Host.Clipboard.SetText(PathHelpers.ToPosix(ParentFile.GetFullPath())));
      }
    }

    public ICommand CopyRelativePathPosixCommand {
      get {
        return CommandDelegate.Create(sender => Host.Clipboard.SetText(PathHelpers.ToPosix(ParentFile.GetRelativePath())));
      }
    }

    public ICommand OpenContainingFolderCommand {
      get {
        return CommandDelegate.Create(sender => Host.WindowsExplorer.OpenContainingFolder(ParentFile.GetFullPath()));
      }
    }

    #endregion
  }
}
