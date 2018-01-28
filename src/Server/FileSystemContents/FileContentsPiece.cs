// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  /// <summary>
  /// The most basic piece of contents that can be searched.
  /// There is at least one instance per searchable file, and
  /// there may be more than one if the file is large enough.
  /// </summary>
  public struct FileContentsPiece {
    private readonly FileName _fileName;
    private readonly FileContents _fileContents;
    private readonly int _fileId;
    private readonly TextRange _textRange;

    public FileContentsPiece(FileName fileName, FileContents fileContents, int fileId, TextRange textRange) {
      _fileName = fileName;
      _fileContents = fileContents;
      _fileId = fileId;
      _textRange = textRange;
    }

    /// <summary>
    /// The file name of the file this piece if part of.
    /// </summary>
    public FileName FileName => _fileName;

    public FileContents FileContents => _fileContents;

    /// <summary>
    /// A unique identifier of the file this piece is part of. This ID is
    /// redundant with <see cref="FileName"/>, it is only needed for
    /// performance, as comparing integers for equality is faster than comparing
    /// filenames.
    /// </summary>
    public int FileId => _fileId;

    /// <summary>
    /// The number of bytes in this piece. This is used for debugging only (i.e.
    /// displaying # of bytes allocated).
    /// </summary>
    public int ByteLength => _textRange.Length * _fileContents.CharacterSize;

    /// <summary>
    /// Find all occurrences of a search term passed in <paramref
    /// name="compiledTextSearchData"/>.
    /// </summary>
    public IList<TextRange> FindAll(CompiledTextSearchData compiledTextSearchData, IOperationProgressTracker progressTracker) {
      return _fileContents.FindAll(compiledTextSearchData, _textRange, progressTracker);
    }
  }
}