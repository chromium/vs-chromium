// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Projects;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemTreeSnapshot"/> snapshot.
  /// </summary>
  public class FileDatabase : IFileDatabase {
    private readonly IDictionary<FileName, FileData> _files;
    private readonly IDictionary<DirectoryName, DirectoryData> _directories;
    private readonly ICollection<ISearchableContents> _searchableContentsCollection;
    private readonly long _searchableFileCount;

    public FileDatabase(IDictionary<FileName, FileData> files,
                        IDictionary<DirectoryName, DirectoryData> directories,
                        ICollection<ISearchableContents> searchableContentsCollection) {
      _files = files;
      _directories = directories;
      _searchableContentsCollection = searchableContentsCollection;
      _searchableFileCount = searchableContentsCollection.GroupBy(x => x.FileId).Count();
    }

    /// <summary>
    /// Returns the list of files with their associated <see cref="FileData"/>.
    /// Note that some instances of <see cref="FileData"/> may have their <see
    /// cref="FileData.Contents"/> property set to null.
    /// </summary>
    public IDictionary<FileName, FileData> Files { get { return _files; } }

    /// <summary>
    /// Returns the list of filenames suitable for file name search.
    /// </summary>
    public ICollection<FileName> FileNames { get { return _files.Keys; } }

    /// <summary>
    /// Returns the list of directory names suitable for directory name search.
    /// </summary>
    public ICollection<DirectoryName> DirectoryNames { get { return _directories.Keys; } }

    public ICollection<ISearchableContents> SearchableContentsCollection {
      get { return _searchableContentsCollection; }
    }

    public long SearchableFileCount {
      get { return _searchableFileCount; }
    }

    public IEnumerable<FileExtract> GetFileExtracts(FileName filename, IEnumerable<FilePositionSpan> spans) {
      var fileData = GetFileData(filename);
      if (fileData == null)
        return Enumerable.Empty<FileExtract>();

      var contents = fileData.Contents;
      if (contents == null)
        return Enumerable.Empty<FileExtract>();

      return contents.GetFileExtracts(spans);
    }

    public bool IsContainedInSymLink(DirectoryName name) {
      DirectoryData entry;
      if (!_directories.TryGetValue(name, out entry))
        return false;

      if (entry.DirectoryEntry.IsSymLink)
        return true;

      var parent = entry.DirectoryName.Parent;
      if (parent == null)
        return false;

      return IsContainedInSymLink(parent);
    }

    /// <summary>
    /// Return the <see cref="FileData"/> instance associated to <paramref name="filename"/> or null if not present.
    /// </summary>
    private FileData GetFileData(FileName filename) {
      FileData result;
      Files.TryGetValue(filename, out result);
      return result;
    }
  }
}
