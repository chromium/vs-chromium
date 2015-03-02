// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class FilePositionViewModel : CodeSearchItemViewModelBase {
    private readonly FileEntryViewModel _parentFile;
    private readonly FilePositionSpan _matchPosition;
    private FileExtract _extractPosition;

    public FilePositionViewModel(ICodeSearchController controller, FileEntryViewModel parentFile, FilePositionSpan matchPosition)
      : base(controller, parentFile, false) {
      _parentFile = parentFile;
      _matchPosition = matchPosition;
    }

    public FileEntryViewModel ParentFile { get { return _parentFile; } }

    public int Position { get { return _matchPosition.Position; } }

    public int Length { get { return _matchPosition.Length; } }

    public string Path {
      get {
        return ParentFile.Path;
      }
    }

    public override string DisplayText {
      get {
        if (_extractPosition != null) {
          return string.Format("{0} ({1}, {2})", _extractPosition.Text.Trim(), _extractPosition.LineNumber + 1, _extractPosition.ColumnNumber + 1);
        } else {
          return string.Format("File offset {0}", Position);
        }
      }
    }

    public string TextBeforeMatch {
      get {
        if (_extractPosition == null)
          return "";

        // [extract - match - extract]
        var offset = 0;
        var length = _matchPosition.Position - _extractPosition.Offset;
        return _extractPosition.Text.Substring(offset, length).TrimStart();
      }
    }

    public string MatchText {
      get {
        if (_extractPosition == null)
          return DisplayText;
        // [extract - match - extract]
        var offset = _matchPosition.Position - _extractPosition.Offset;
        var length = _matchPosition.Length;
        return _extractPosition.Text.Substring(offset, length);
      }
    }

    public string TextAfterMatch {
      get {
        if (_extractPosition == null)
          return "";
        // [extract - match - extract]
        var offset = _matchPosition.Position + _matchPosition.Length - _extractPosition.Offset;
        var length = _extractPosition.Length - offset;
        var text = _extractPosition.Text.Substring(offset, length).TrimEnd();
        return string.Format("{0} ({1}, {2})", text, _extractPosition.LineNumber + 1, _extractPosition.ColumnNumber + 1);
      }
    }


    public override ImageSource ImageSourcePath {
      get {
        return StandarImageSourceFactory.GetImage("FileGo");
      }
    }

    public void SetTextExtract(FileExtract value) {
      _extractPosition = value;
      OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.DisplayText));
      OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.TextBeforeMatch));
      OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.MatchText));
      OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.TextAfterMatch));
    }

    #region Command Handlers

    public ICommand OpenCommand {
      get {
        return CommandDelegate.Create(sender => Controller.OpenFileInEditor(ParentFile, new Span(Position, Length)));
      }
    }

    public ICommand OpenWithCommand {
      get {
        return CommandDelegate.Create(sender => Controller.OpenFileInEditor(ParentFile, new Span(Position, Length)));
      }
    }

    public ICommand CopyCommand {
      get {
        return CommandDelegate.Create(sender => Controller.Clipboard.SetText(DisplayText));
      }
    }

    #endregion
  }
}
