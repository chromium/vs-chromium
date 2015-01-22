// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemTreeSnapshot"/> snapshot.
  /// </summary>
  public class FileDatabase : IFileDatabase {
    private readonly IDictionary<FileName, FileData> _files;
    private readonly IDictionary<DirectoryName, DirectoryData> _directories;
    private readonly IList<IFileContentsPiece> _fileContentsPieces;
    private readonly long _searchableFileCount;
    private FileName[] _filesArray;
    private DirectoryName[] _directoriesArray;

    public FileDatabase(IDictionary<FileName, FileData> files,
                        IDictionary<DirectoryName, DirectoryData> directories,
                        IList<IFileContentsPiece> fileContentsPieces,
                        long searchableFileCount) {
      _files = files;
      _filesArray = files.Keys.ToArray();
      _directories = directories;
      _directoriesArray = directories.Keys.ToArray();
      _fileContentsPieces = fileContentsPieces;
      _searchableFileCount = searchableFileCount;
    }

    public IDictionary<FileName, FileData> Files {
      get {
        return _files;
      }
    }

    public IDictionary<DirectoryName, DirectoryData> Directories {
      get {
        return _directories;
      }
    }

    public IList<FileName> FileNames {
      get {
        return _filesArray;
      }
    }

    public IList<DirectoryName> DirectoryNames {
      get {
        return _directoriesArray;
      }
    }

    public IList<IFileContentsPiece> FileContentsPieces {
      get { return _fileContentsPieces.ToArray(); }
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
