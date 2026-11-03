// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Server.Search;
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
    private readonly Lazy<long> _totalFileContentsLength;
    private readonly Lazy<HashSet<DirectoryName>> _directoriesInSymLinks;
    private FileWithContents[] _fileArray;

    public FileDatabaseSnapshot(IDictionary<FullPath, string> projectHashes, 
      IDictionary<DirectoryName, DirectoryData> directories,
      IDictionary<FileName, FileWithContents> files,
      IList<FileName> fileNames = null,
      IList<FileContentsPiece> fileContentsPieces = null) {
      _projectHashes = projectHashes;
      _directories = directories;
      _files = files;
      // Note: "fileNames" can be passed when the keys of "files" are the same as in a previous
      // snapshot (only file contents changed), to avoid recomputing the list.
      _fileNames = fileNames != null ? new Lazy<IList<FileName>>(fileNames) : new Lazy<IList<FileName>>(CreateFileNames);
      // Note: "fileContentsPieces" can be passed when they are known (e.g. updated from a previous
      // snapshot), to avoid recomputing them.
      _fileContentsPieces = fileContentsPieces != null
        ? new Lazy<IList<FileContentsPiece>>(fileContentsPieces)
        : new Lazy<IList<FileContentsPiece>>(CreateFilePieces, LazyThreadSafetyMode.ExecutionAndPublication);
      _searchableFileCount = new Lazy<long>(CountFilesWithContents);
      _totalFileContentsLength = new Lazy<long>(ComputeTotalFileContentsLength);
      _directoriesInSymLinks = new Lazy<HashSet<DirectoryName>>(ComputeDirectoriesInSymLinks);
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
    public long FileNameCount => _files.Count;
    public long TotalFileContentsLength => _totalFileContentsLength.Value;

    public IEnumerable<FileExtract> GetFileExtracts(FileName filename, IEnumerable<FilePositionSpan> spans,
      int maxLength) {
      var contents = GetFileContents(filename);
      if (contents == null) {
        return Enumerable.Empty<FileExtract>();
      }

      return contents.GetFileExtracts(maxLength, spans);
    }

    public void PrecomputeSearchData() {
      _ = _fileNames.Value;
      _ = _fileContentsPieces.Value;
      _ = _searchableFileCount.Value;
      _ = _totalFileContentsLength.Value;
      _ = _directoriesInSymLinks.Value;
      // Only needed to compute the values above.
      _fileArray = null;
    }

    public bool IsContainedInSymLink(DirectoryName name) {
      // Note: Computed once per snapshot: a single hash lookup per call, and no lookup
      // at all when there is no symbolic link (the common case), instead of a walk up the parent
      // chain with a lookup per parent for each file of each query.
      var directoriesInSymLinks = _directoriesInSymLinks.Value;
      if (directoriesInSymLinks.Count == 0 || name == null)
        return false;
      return directoriesInSymLinks.Contains(name);
    }

    /// <summary>
    /// The directories of the snapshot that are symbolic links, or are contained in one.
    /// </summary>
    private HashSet<DirectoryName> ComputeDirectoriesInSymLinks() {
      var result = new HashSet<DirectoryName>();
      if (!_directories.Values.Any(x => x.IsSymLink))
        return result;

      foreach (var directory in _directories.Keys) {
        if (IsContainedInSymLinkHelper(_directories, directory))
          result.Add(directory);
      }
      return result;
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

    /// <summary>
    /// The values of <see cref="Files"/>, computed once and shared by the computations below (a
    /// single enumeration of the table).
    /// </summary>
    private FileWithContents[] GetFileArray() {
      // Note: Benign race (the array would be computed twice).
      return _fileArray ??= CreateFileArray();
    }

    private FileWithContents[] CreateFileArray() {
      var table = _files as SlimHashTable<FileName, FileWithContents>;
      return table != null ? table.ValuesToArray() : _files.Values.ToArray();
    }

    private IList<FileName> CreateFileNames() {
      var files = GetFileArray();
      var result = new FileName[files.Length];
      for (var i = 0; i < files.Length; i++) {
        result[i] = files[i].FileName;
      }
      return result;
    }

    private IList<FileContentsPiece> CreateFilePieces() {
      return FileDatabaseBuilder.CreateFilePieces(GetFileArray());
    }

    private long CountFilesWithContents() {
      var files = GetFileArray();
      long count = 0;
      for (var i = 0; i < files.Length; i++) {
        if (FileDatabaseBuilder.FileHasContents(files[i]))
          count++;
      }
      return count;
    }

    private long ComputeTotalFileContentsLength() {
      var files = GetFileArray();
      long total = 0;
      for (var i = 0; i < files.Length; i++) {
        if (files[i].Contents != null)
          total += files[i].Contents.ByteLength;
      }
      return total;
    }
  }
}
