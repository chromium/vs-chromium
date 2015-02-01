// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
  public interface IFileDatabase {
    /// <summary>
    /// Returns the list of filenames suitable for file name search.
    /// 
    /// Note: Return type is IList to allow efficient partitioning with
    /// ".AsParallel()".
    /// </summary>
    IList<FileName> FileNames { get; }

    /// <summary>
    /// The list of directory names suitable for directory name search.
    /// 
    /// Note: Return type is IList to allow efficient partitioning with
    /// ".AsParallel()".
    /// </summary>
    IList<DirectoryName> DirectoryNames { get; }

    /// <summary>
    /// Returns the list of entities with text contents suitable for text search.
    /// For large files, there is more than one <see cref="IFileContentsPiece"/>
    /// entry per file.
    /// 
    /// Note: Return type is IList to allow efficient partitioning with
    /// ".AsParallel()".
    /// </summary>
    IList<IFileContentsPiece> FileContentsPieces { get; }

    /// <summary>
    /// The total number of file which can be searched for contents.
    /// </summary>
    long SearchableFileCount { get; }

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
    bool IsContainedInSymLink(FileSystemName name);
  }
}