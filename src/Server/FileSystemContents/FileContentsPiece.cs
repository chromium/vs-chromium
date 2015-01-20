// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  public class FileContentsPiece : IFileContentsPiece {
    private readonly FileName _fileName;
    private readonly FileContents _fileContents;
    private readonly int _fileId;
    private readonly TextRange _textRange;

    public FileContentsPiece(FileName fileName, FileContents fileContents,int fileId, TextRange textRange) {
      _fileName = fileName;
      _fileContents = fileContents;
      _fileId = fileId;
      _textRange = textRange;
    }

    public FileName FileName {
      get { return _fileName; }
    }

    public int FileId {
      get { return _fileId; }
    }

    public List<FilePositionSpan> Search(
      SearchContentsData searchContentsData,
      IOperationProgressTracker progressTracker) {
      return _fileContents.Search(
        _textRange,
        searchContentsData,
        progressTracker);
    }
  }
}