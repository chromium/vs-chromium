// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
  /// <para>
  /// Layout (compact, for trees of millions of files): the file names and their contents are two
  /// parallel arrays, the files of each directory being a contiguous range sorted by name (see
  /// <see cref="Directories"/>), so that a file is found with a lookup of its directory and a
  /// binary search (no table of all the files). The pieces searched by code searches refer to the
  /// files by index (see <see cref="FilePieceList"/>).
  /// </para>
  /// </summary>
  public class FileDatabaseSnapshot : IFileDatabaseSnapshot {
    private readonly IDictionary<FullPath, string> _projectHashes;
    private readonly Dictionary<DirectoryName, FileRange> _directories;
    private readonly FileName[] _fileNames;
    /// <summary>
    /// Parallel to <see cref="_fileNames"/>. Note: Shared with the snapshots created for
    /// modified files (see <see cref="DangerousSetContents"/>).
    /// </summary>
    private readonly FileContents[] _contents;
    private readonly Lazy<FilePieceList> _fileContentsPieces;
    private readonly Lazy<long> _searchableFileCount;
    private readonly Lazy<long> _totalFileContentsLength;

    public FileDatabaseSnapshot(IDictionary<FullPath, string> projectHashes,
      Dictionary<DirectoryName, FileRange> directories,
      FileName[] fileNames,
      FileContents[] contents,
      FilePieceList fileContentsPieces = null) {
      _projectHashes = projectHashes;
      _directories = directories;
      _fileNames = fileNames;
      _contents = contents;
      // Note: "fileContentsPieces" can be passed when they are known (e.g. the pieces of the
      // previous snapshot, when only the contents of small files changed).
      _fileContentsPieces = fileContentsPieces != null
        ? new Lazy<FilePieceList>(fileContentsPieces)
        : new Lazy<FilePieceList>(() => FileDatabaseBuilder.CreateFilePieces(_fileNames, _contents), LazyThreadSafetyMode.ExecutionAndPublication);
      _searchableFileCount = new Lazy<long>(CountFilesWithContents);
      _totalFileContentsLength = new Lazy<long>(ComputeTotalFileContentsLength);
    }

    public static FileDatabaseSnapshot Empty() {
      return new FileDatabaseSnapshot(new Dictionary<FullPath, string>(), new Dictionary<DirectoryName, FileRange>(),
        Array.Empty<FileName>(), Array.Empty<FileContents>(), FilePieceList.Empty);
    }

    public IDictionary<FullPath, string> ProjectHashes => _projectHashes;

    /// <summary>
    /// The range of the files of each directory (with at least one file) in <see cref="FileNames"/>.
    /// </summary>
    public Dictionary<DirectoryName, FileRange> Directories => _directories;

    /// <summary>
    /// The names of all the files, some of them with actual searchable contents, some of them
    /// with no contents (i.e. if they are not searchable, or binary files or could not be read
    /// from disk due to some error). See <see cref="GetContents"/>.
    /// </summary>
    public IList<FileName> FileNames => _fileNames;
    public FileName[] FileNameArray => _fileNames;
    public FileContents[] ContentsArray => _contents;
    public IList<FileContentsPiece> FileContentsPieces => _fileContentsPieces.Value;
    public FilePieceList FilePieces => _fileContentsPieces.Value;
    public long SearchableFileCount => _searchableFileCount.Value;
    public long FileNameCount => _fileNames.Length;
    public long TotalFileContentsLength => _totalFileContentsLength.Value;

    /// <summary>
    /// The contents of the file at <paramref name="index"/> in <see cref="FileNames"/> (null if
    /// the file is not searchable).
    /// </summary>
    public FileContents GetContents(int index) => _contents[index];

    /// <summary>
    /// Returns the index of <paramref name="fileName"/> in <see cref="FileNames"/>, or false if
    /// the file is not in this snapshot.
    /// </summary>
    public bool TryGetFileIndex(FileName fileName, out int index) {
      index = -1;
      if (fileName.Parent == null || !_directories.TryGetValue(fileName.Parent, out var range))
        return false;
      // The files of a directory are sorted by name (see FileDatabaseBuilder).
      var low = range.Start;
      var high = range.Start + range.Count - 1;
      while (low <= high) {
        var middle = low + ((high - low) >> 1);
        var comparison = SystemPathComparer.CompareNames(_fileNames[middle].Name, fileName.Name);
        if (comparison == 0) {
          index = middle;
          return true;
        }
        if (comparison < 0)
          low = middle + 1;
        else
          high = middle - 1;
      }
      return false;
    }

    /// <summary>
    /// Replaces the contents of the file at <paramref name="index"/>. Note: Concurrency: there
    /// are potentially many readers (searches), and one writer (the indexing thread): the update
    /// is an atomic reference assignment. Searches using this snapshot (or a snapshot sharing its
    /// arrays) see the old or the new contents of the file, both valid.
    /// </summary>
    public void DangerousSetContents(int index, FileContents contents) {
      Volatile.Write(ref _contents[index], contents);
    }

    public IEnumerable<FileExtract> GetFileExtracts(FileName filename, IEnumerable<FilePositionSpan> spans,
      int maxLength) {
      var contents = TryGetFileIndex(filename, out var index) ? _contents[index] : null;
      if (contents == null) {
        return Enumerable.Empty<FileExtract>();
      }

      return contents.GetFileExtracts(maxLength, spans);
    }

    public void PrecomputeSearchData() {
      _ = _fileContentsPieces.Value;
      _ = _searchableFileCount.Value;
      _ = _totalFileContentsLength.Value;
    }

    public bool IsContainedInSymLink(DirectoryName name) {
      // Note: A flag of the directory name, computed when the name is created (no lookup).
      return name != null && name.IsInSymLink;
    }

    private long CountFilesWithContents() {
      long count = 0;
      for (var i = 0; i < _contents.Length; i++) {
        if (FileDatabaseBuilder.HasContents(_contents[i]))
          count++;
      }
      return count;
    }

    private long ComputeTotalFileContentsLength() {
      long total = 0;
      for (var i = 0; i < _contents.Length; i++) {
        if (_contents[i] != null)
          total += _contents[i].ByteLength;
      }
      return total;
    }
  }

  /// <summary>
  /// A range of files in <see cref="FileDatabaseSnapshot.FileNames"/>.
  /// </summary>
  public readonly struct FileRange {
    public FileRange(int start, int count) {
      Start = start;
      Count = count;
    }

    public int Start { get; }
    public int Count { get; }
  }
}
