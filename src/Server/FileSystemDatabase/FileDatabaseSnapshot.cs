// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase.Builder;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemSnapshot"/> snapshot.
  /// </summary>
  public class FileDatabaseSnapshot : IFileDatabaseSnapshot {
    private readonly IDictionary<FullPath, string> _projectHashes;
    private readonly IDictionary<DirectoryName, DirectoryData> _directories;
    private readonly IDictionary<FileName, FileWithContents> _files;
    private readonly Lazy<IList<FileName>> _fileNames;
    private readonly Lazy<IList<FileContentsPiece>> _fileContentsPieces;
    private readonly Lazy<long> _searchableFileCount;

    public FileDatabaseSnapshot(IDictionary<FullPath, string> projectHashes, 
      IDictionary<DirectoryName, DirectoryData> directories,
      IDictionary<FileName, FileWithContents> files) {
      _projectHashes = projectHashes;
      _directories = directories;
      _files = files;
      _fileNames = new Lazy<IList<FileName>>(CreateFileNames);
      _fileContentsPieces = new Lazy<IList<FileContentsPiece>>(CreateFilePieces, LazyThreadSafetyMode.ExecutionAndPublication);
      _searchableFileCount = new Lazy<long>(CountFilesWithContents);
    }

    public IDictionary<FullPath, string> ProjectHashes => _projectHashes;
    public IDictionary<DirectoryName, DirectoryData> Directories => _directories;

    /// <summary>
    /// List of files, some of them with actual searchable contents, some of them
    /// with no contents (i.e. if they are not searchable, or binary files or could not
    /// be read from disk due to some error).
    /// </summary>
    public IDictionary<FileName, FileWithContents> Files => _files;

    /// <summary>
    /// The list of <see cref="FileName"/>, a projection of <see cref="Files"/>.Keys, exposed
    /// as an <see cref="IList{T}"/> for performance reaons, i.e. to allow efficient parallel search.
    /// </summary>
    public IList<FileName> FileNames => _fileNames.Value;
    public IList<FileContentsPiece> FileContentsPieces => _fileContentsPieces.Value;
    public long SearchableFileCount => _searchableFileCount.Value;

    public IEnumerable<FileExtract> GetFileExtracts(FileName filename, IEnumerable<FilePositionSpan> spans,
      int maxLength) {
      var contents = GetFileContents(filename);
      if (contents == null) {
        return Enumerable.Empty<FileExtract>();
      }

      return contents.GetFileExtracts(maxLength, spans);
    }

    public bool IsContainedInSymLink(DirectoryName name) {
      return IsContainedInSymLinkHelper(_directories, name);
    }

    public static bool IsContainedInSymLinkHelper(IDictionary<DirectoryName, DirectoryData> directories, FileName name) {
      return IsContainedInSymLinkHelper(directories, name.Parent);
    }

    public static bool IsContainedInSymLinkHelper(IDictionary<DirectoryName, DirectoryData> directories, DirectoryName name) {
      if (name == null)
        return false;

      DirectoryData directoryData;
      if (!directories.TryGetValue(name, out directoryData))
        return false;

      if (directoryData.IsSymLink)
        return true;

      var parent = name.Parent;
      if (parent == null)
        return false;

      return IsContainedInSymLinkHelper(directories, parent);
    }

    /// <summary>
    /// Return the <see cref="FileContents"/> instance associated to <paramref name="filename"/> or null if not present.
    /// </summary>
    private FileContents GetFileContents(FileName filename) {
      FileWithContents result;
      if (!Files.TryGetValue(filename, out result)) {
        return null;
      }
      return result.Contents;
    }

    private IList<FileName> CreateFileNames() {
      return _files.Keys.ToArray();
    }

    private IList<FileContentsPiece> CreateFilePieces() {
      return FileDatabaseBuilder.CreateFilePieces(_files.Values);
    }

    private long CountFilesWithContents() {
      return _files.Values.Where(FileDatabaseBuilder.FileHasContents).Count();
    }
  }
}
