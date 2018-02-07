// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
  public interface IFileDatabaseSnapshot {
    /// <summary>
    /// Returns the list of filenames suitable for file name search.
    /// 
    /// Note: Return type is IList to allow efficient partitioning with
    /// ".AsParallel()".
    /// </summary>
    IList<FileName> FileNames { get; }

    /// <summary>
    /// Returns the list of entities with text contents suitable for text search.
    /// For large files, there is more than one <see cref="FileContentsPiece"/>
    /// entry per file.
    /// 
    /// Note: Return type is IList to allow efficient partitioning with
    /// ".AsParallel()".
    /// </summary>
    IList<FileContentsPiece> FileContentsPieces { get; }

    /// <summary>
    /// The total number of file which can be searched for contents.
    /// This is the same value of the number of unique files contained in
    /// <see cref="FileContentsPieces"/>
    /// </summary>
    long SearchableFileCount { get; }

    /// <summary>
    /// The total number of file names in <see cref="FileNames"/>. This value
    /// is faster to compute than <see cref="FileNames"/>.Count
    /// </summary>
    long FileNameCount { get; }

    /// <summary>
    /// The total number of bytes contained in the files that can be searched.
    /// This is the same value as the sum of bytes from the pieces contained
    /// in <see cref="FileContentsPieces"/>, but faster to compute.
    /// </summary>
    long TotalFileContentsLength { get; }

    /// <summary>
    /// Returns the list of text extracts for a given <paramref
    /// name="fileName"/> and list of <paramref name="spans"/>. <paramref
    /// name="maxLength"/> defines the maximum number of characters to include
    /// in each file extract if the text line corresponding to the span is too
    /// long.
    /// </summary>
    IEnumerable<FileExtract> GetFileExtracts(FileName fileName, IEnumerable<FilePositionSpan> spans, int maxLength);

    /// <summary>
    /// Returns true if the file or directory <paramref name="name"/> is
    /// containted in a symlink directory.
    /// </summary>
    bool IsContainedInSymLink(DirectoryName name);
  }
}