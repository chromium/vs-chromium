// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows.Media;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  public class FilePositionViewModel : TreeViewItemViewModel {
    private readonly FilePositionSpan _position;
    private FileExtract _fileExtract;

    public FilePositionViewModel(
        ITreeViewItemViewModelHost host,
        TreeViewItemViewModel parent,
        FilePositionSpan position)
        : base(host, parent, false) {
      this._position = position;
    }

    public int Position {
      get {
        return this._position.Position;
      }
    }

    public int Length {
      get {
        return this._position.Length;
      }
    }

    public string Path {
      get {
        var parent = ParentViewModel as FileEntryViewModel;
        if (parent == null)
          return null;
        return parent.Path;
      }
    }

    public string DisplayText {
      get {
        if (this._fileExtract != null) {
          return string.Format("{0} ({1}, {2})", this._fileExtract.Text.Trim(), this._fileExtract.LineNumber + 1, this._fileExtract.ColumnNumber + 1);
        } else {
          return string.Format("File offset {0}", Position);
        }
      }
    }

    public override ImageSource ImageSourcePath {
      get {
        return StandarImageSourceFactory.GetImage("FileGo");
      }
    }

    public void SetTextExtract(FileExtract value) {
      this._fileExtract = value;
      OnPropertyChanged("DisplayText");
    }
  }
}
