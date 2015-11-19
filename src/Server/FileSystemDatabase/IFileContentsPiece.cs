// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// The most basic piece of contents that can be searched.
  /// There is at least one instance per searchable file, and
  /// there may be more than one if the file is large enough.
  /// </summary>
  public interface IFileContentsPiece {
    /// <summary>
    /// The file name of the file this piece if part of.
    /// </summary>
    FileName FileName { get; }
    /// <summary>
    /// A unique identifier of the file this piece is part of. This ID is
    /// redundant with <see cref="FileName"/>, it is only needed for
    /// performance, as comparing integers for equality is faster than comparing
    /// filenames.
    /// </summary>
    int FileId { get; }
    /// <summary>
    /// The number of bytes in this piece. This is used for debugging only (i.e.
    /// displaying # of bytes allocated).
    /// </summary>
    int ByteLength { get; }

    /// <summary>
    /// Find all occurrences of a search term passed in <paramref
    /// name="compiledTextSearchData"/>.
    /// </summary>
    IList<TextRange> FindAll(
      CompiledTextSearchData compiledTextSearchData,
      IOperationProgressTracker progressTracker);
  }
}