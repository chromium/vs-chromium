// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase {
  public interface IFileDatabase {
    /// <summary>
    /// Returns the list of filenames suitable for file name search.
    /// </summary>
    ICollection<FileName> FileNames { get; }

    /// <summary>
    /// Returns the list of directory names suitable for directory name search.
    /// </summary>
    ICollection<DirectoryName> DirectoryNames { get; }

    /// <summary>
    /// Returns the list of files with text contents suitable for text search.
    /// </summary>
    ICollection<FileData> FilesWithContents { get; }

    /// <summary>
    /// Returns the list of text extracts for a given <paramref
    /// name="fileName"/> and list of <paramref name="spans"/>.
    /// </summary>
    IEnumerable<FileExtract> GetFileExtracts(FileName fileName, IEnumerable<FilePositionSpan> spans);

    /// <summary>
    /// Atomically updates the file contents of <paramref name="projectFile"/>
    /// with the new file contents on disk. This method violates the "pure
    /// snapshot" semantics but enables efficient updates for the most common
    /// type of file change events.
    /// </summary>
    void UpdateFileContents(Tuple<IProject, FileName> projectFile);

    /// <summary>
    /// Returns true if <paramref name="name"/> or any of its parent is a symlink directory.
    /// </summary>
    bool IsContainedInSymLink(DirectoryName name);
  }
}