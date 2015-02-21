// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemContents;
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
    FileName FileName { get; }
    FileContents FileContents { get; }
    int FileId { get; }

    IList<TextRange> FindAll(
      CompiledTextSearchData compiledTextSearchData,
      IOperationProgressTracker progressTracker);
  }
}