// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemDatabase {
  public class FileContentsPiece : IFileContentsPiece {
    private readonly FileData _fileData;
    private readonly int _fileId;
    private readonly long _offset;
    private readonly long _length;

    public FileContentsPiece(FileData fileData, int fileId, long offset, long length) {
      _fileData = fileData;
      _fileId = fileId;
      _offset = offset;
      _length = length;
    }

    public FileData FileData {
      get { return _fileData; }
    }

    public FileName FileName {
      get { return FileData.FileName; }
    }

    public int FileId {
      get { return _fileId; }
    }

    public long ByteLength {
      get { return _length; }
    }

    public List<FilePositionSpan> Search(
      SearchContentsData searchContentsData,
      IOperationProgressTracker progressTracker) {
      return FileData.Contents.Search(
        FileData.FileName,
        _offset,
        _length,
        searchContentsData,
        progressTracker);
    }
  }
}