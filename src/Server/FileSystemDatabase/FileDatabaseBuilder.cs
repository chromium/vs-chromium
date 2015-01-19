// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase {
  public class FileDatabaseBuilder {
    private const int ChunkSize = 100 * 1024;
    private readonly IFileSystem _fileSystem;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private FileNameDictionary<FileInfo> _files;
    private Dictionary<DirectoryName, DirectoryData> _directories;

    public FileDatabaseBuilder(IFileSystem fileSystem, IFileContentsFactory fileContentsFactory, IProgressTrackerFactory progressTrackerFactory) {
      _fileSystem = fileSystem;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public IFileDatabase Build(IFileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot) {
      using (var logger = new TimeElapsedLogger("Building file database from previous one and file system tree snapshot")) {
        var fileDatabase = (FileDatabase)previousFileDatabase;
        // Compute list of files from tree
        ComputeFileCollection(newSnapshot);

        // Merge old state in new state
        var fileContentsMemoization = new FileContentsMemoization();
        TransferUnchangedFileContents(fileDatabase, fileContentsMemoization);

        // Load file contents into newState
        ReadMissingFileContents(fileContentsMemoization);

        Logger.Log("{0}{1:n0} unique file contents remaining in memory after memoization of {2:n0} files.",
            logger.Indent,
            fileContentsMemoization.Count,
            _files.Values.Count(x => x.FileData.Contents != null));

        return CreateFileDatabse();
      }
    }

    public IFileDatabase BuildWithChangedFiles(IFileDatabase previousFileDatabase, IEnumerable<Tuple<IProject, FileName>> changedFiles) {
      using (new TimeElapsedLogger("Building file database from previous one and list of changed files")) {
        var fileDatabase = (FileDatabase)previousFileDatabase;

        // Update file contents of file data entries of changed files.
        var contentsUpdated = false;
        changedFiles.ForAll(x => {
          if (x.Item1.IsFileSearchable(x.Item2) && fileDatabase.Files.ContainsKey(x.Item2)) {
            var newContents = _fileContentsFactory.GetFileContents(x.Item2.FullPath);
            fileDatabase.Files[x.Item2].UpdateContents(newContents);
            contentsUpdated = true;
          }
        });

        if (!contentsUpdated)
          return previousFileDatabase;

        // Return new file database with updated file contents.
        var filesWithContents = GetFilesWithContents(fileDatabase.Files.Values);
        return new FileDatabase(
          fileDatabase.Files,
          fileDatabase.Directories,
          CreateSearchableContentsCollection(filesWithContents),
          filesWithContents.Count);
      }
    }

    private FileDatabase CreateFileDatabse() {
      using (new TimeElapsedLogger("Freezing FileDatabase state")) {
        var files = _files.ToDictionary(x => x.Key, x => x.Value.FileData);
        var directories = _directories;
        var filesWithContents = GetFilesWithContents(files.Values);
        var searchableContentsCollection = CreateSearchableContentsCollection(filesWithContents);
        return new FileDatabase(files, directories, searchableContentsCollection, filesWithContents.Count);
      }
    }

    /// <summary>
    /// Note: This code inside this method is not the cleanest, but it is
    /// written in a way that tries to minimiz the # of large array allocations.
    /// </summary>
    private static IList<ISearchableContents> CreateSearchableContentsCollection(ICollection<FileData> filesWithContents) {
      // Factory for file identifiers
      int currentFileId = 0;
      Func<int> fileIdFactory = () => currentFileId++;

      // Predicate to figure out if a file is "small"
      Func<FileData, bool> isSmallFile = x => x.Contents.ByteLength <= ChunkSize;

      // Count the total # of small and large files, while splitting large files
      // into their fragments.
      var smallFilesCount = 0;
      var largeFiles = new List<SearchableContents>(filesWithContents.Count / 100);
      foreach (var fileData in filesWithContents) {
        if (isSmallFile(fileData)) {
          smallFilesCount++;
        } else {
          var splitFileContents = SplitFileContents(fileData, fileIdFactory());
          largeFiles.AddRange(splitFileContents);
        }
      }
      var totalFileCount = smallFilesCount + largeFiles.Count;

      // Store elements in their partitions
      // # of partitions = # of logical processors
      var fileContents = new SearchableContents[totalFileCount];
      var generator = new PartitionIndicesGenerator(
        totalFileCount,
        Environment.ProcessorCount);

      // Store small files
      foreach (var fileData in filesWithContents) {
        if (isSmallFile(fileData)) {
          var item = new SearchableContents(
            fileData,
            fileIdFactory(),
            0,
            fileData.Contents.ByteLength);
          fileContents[generator.Next()] = item;
        }
      }
      // Store large files
      foreach (var item in largeFiles) {
        fileContents[generator.Next()] = item;
      }

      Debug.Assert(fileContents.All(x => x != null));
      Debug.Assert(fileContents.Aggregate(0L, (c, x) => c + x.ByteLength) ==
        filesWithContents.Aggregate(0L, (c, x) => c + x.Contents.ByteLength));
#if false
      for (var p = 0; p < partitionCount; p++) {
        long weight = 0;
        for (var i = 0; i < partitionSize; i++) {
          var index = (p * partitionSize) + i;
          if (index >= fileContents.Length)
            break;
          weight += fileContents[index].ByteLength;
        }
        Logger.Log("Partition {0} has a weigth of {1:n0}", p, weight);
      }
#endif
      return fileContents;
    }

    class PartitionIndicesGenerator {
      private readonly int _count;
      // Size of paritions (rounded up)
      private readonly int _partitionSize;
      // Last index of partitions that are of filled up to partition size
      private readonly int _fullPartitionsEndIndex;
      private int _currentIndex;

      public PartitionIndicesGenerator(int count, int partitionCount) {
        _count = count;
        _partitionSize = (count + partitionCount - 1) / partitionCount;
        int fullPartitionCount = (_partitionSize == 1 ? count : count % (_partitionSize - 1));
        _fullPartitionsEndIndex = fullPartitionCount * _partitionSize;
      }

      public int Next() {
        var result = _currentIndex;
        _currentIndex = NextIndex(_currentIndex);
        return result;
      }

      private int NextIndex(int index) {
        // Move to the next partition
        if (index < _fullPartitionsEndIndex)
          index += _partitionSize;
        else
          index += _partitionSize - 1;

        // If we reach past limit, we move "up" to the next row in partitions
        if (index >= _count) {
          index -= _count;
          index++;
        }
        return index;
      }
    }

    private static List<FileData> GetFilesWithContents(ICollection<FileData> files) {
      // Create filesWithContents with minimum memory allocations and copying.
      var filesWithContents = new List<FileData>(files.Count);
      filesWithContents.AddRange(files.Where(x => x.Contents != null && x.Contents.ByteLength > 0));
      return filesWithContents;
    }

    private static IList<ISearchableContents> CreateSearchableContentsCollection2(ICollection<FileData> filesWithContents) {
      int currentFileId = 0;
      Func<int> fileIdFactory = () => currentFileId++;

      // Predicate to figure out if a file is "small"
      Func<FileData, bool> isSmallFile = x => x.Contents.ByteLength <= ChunkSize;

      var smallFiles = filesWithContents
        .Where(isSmallFile)
        .Select(x => new SearchableContents(x, fileIdFactory(), 0, x.Contents.ByteLength))
        .ToArray();
      var largeFiles =
        filesWithContents.Where(x => !isSmallFile(x))
        .SelectMany(x => SplitFileContents(x, fileIdFactory()))
        .ToArray();
      var allFiles = new List<SearchableContents>();
      allFiles.AddRange(smallFiles);
      allFiles.AddRange(largeFiles);

      // Note: Partitioning evenly ensures that each processor used by PLinq
      // will deal with a partition of equal "weight". In this case, we make
      // sure each partition contains not only the same amount of files, but
      // also (as close to as possible) the same amount of "bytes". For example,
      // if we have 100 files totaling 32MB and 4 processors, we will end up
      // with 4 partitions of (exactly) 25 files totalling (approximately) 8MB
      // each.
      var partitions = allFiles
        .PartitionByChunks(Environment.ProcessorCount);
      var result = new List<ISearchableContents>(allFiles.Count);
      partitions.ForAll(result.AddRange);
      return result;
    }
    /// <summary>
    ///  Create chunks of 100KB for files larger than 100KB.
    /// </summary>
    private static IEnumerable<SearchableContents> SplitFileContents(FileData fileData, int fileId) {
      var chunkOffset = 0L;
      var totalLength = fileData.Contents.ByteLength;
      while (totalLength > 0) {
        // TODO(rpaquay): Be smarter and split around new lines characters.
        var chunkLength = Math.Min(totalLength, ChunkSize);
        yield return new SearchableContents(fileData, fileId, chunkOffset, chunkLength);

        totalLength -= chunkLength;
        chunkOffset += chunkLength;
      }
    }

    private void ComputeFileCollection(FileSystemTreeSnapshot snapshot) {
      using (new TimeElapsedLogger("Computing list of searchable files from FileSystemTree")) {
        var directories = FileSystemSnapshotVisitor.GetDirectories(snapshot).ToList();

        var directoryNames = directories
          .ToDictionary(x => x.Value.DirectoryName, x => x.Value.DirectoryData);

        var searchableFiles = directories
          .AsParallel()
          .SelectMany(x => x.Value.ChildFiles.Select(y => KeyValuePair.Create(x.Key, y)))
          .Select(x => new FileInfo(new FileData(x.Value, null), x.Key.IsFileSearchable(x.Value)))
          .ToList();

        var files = searchableFiles
          .ToDictionary(x => x.FileData.FileName, x => x);

        _files = new FileNameDictionary<FileInfo>(files);
        _directories = directoryNames;
        //Logger.LogMemoryStats();
      }
    }

    private void TransferUnchangedFileContents(FileDatabase oldState, IFileContentsMemoization fileContentsMemoization) {
      using (new TimeElapsedLogger("Checking for out of date files")) {
        IList<FileData> commonOldFiles = GetCommonFiles(oldState).ToArray();
        using (var progress = _progressTrackerFactory.CreateTracker(commonOldFiles.Count)) {
          commonOldFiles
            .AsParallel()
            .Where(oldFileData => {
              if (progress.Step()) {
                progress.DisplayProgress(
                  (i, n) =>
                    string.Format("Checking file timestamp {0:n0} of {1:n0}: {2}", i, n, oldFileData.FileName.FullPath));
              }
              return IsFileContentsUpToDate(oldFileData);
            })
            .ForAll(oldFileData => {
              var contents = fileContentsMemoization.Get(oldFileData.FileName, oldFileData.Contents);
              _files[oldFileData.FileName].FileData.UpdateContents(contents);
            });
        }
      }
    }

    /// <summary>
    /// Reads the content of all file entries that have no content (yet). Returns the # of files read from disk.
    /// </summary>
    private void ReadMissingFileContents(IFileContentsMemoization fileContentsMemoization) {
      using (var logger = new TimeElapsedLogger("Loading file contents from disk")) {
        using (var progress = _progressTrackerFactory.CreateTracker(_files.Count)) {
          _files.Values
            .AsParallel()
            .ForAll(fileInfo => {
              if (progress.Step()) {
                progress.DisplayProgress(
                  (i, n) =>
                    string.Format("Reading file {0:n0} of {1:n0}: {2}", i, n, fileInfo.FileData.FileName.FullPath));
              }
              if (fileInfo.IsSearchable && fileInfo.FileData.Contents == null) {
                var fileContents = _fileContentsFactory.GetFileContents(fileInfo.FileData.FileName.FullPath);
                fileInfo.FileData.UpdateContents(fileContentsMemoization.Get(fileInfo.FileData.FileName, fileContents));
              }
            });
        }

        Logger.Log("{0}Loaded {1:n0} files", logger.Indent, _files.Count);
        Logger.LogMemoryStats(logger.Indent);
      }
    }

    private bool IsFileContentsUpToDate(FileData oldFileData) {
      // TODO(rpaquay): The following File.Exists and File.GetLastWriteTimUtc are expensive operations.
      //  Given we have FileSystemChanged events when files change on disk, we could be smarter here
      // and avoid 99% of these checks in common cases.
      var fi = _fileSystem.GetFileInfoSnapshot(oldFileData.FileName.FullPath);
      if (fi.Exists) {
        var contents = oldFileData.Contents;
        if (contents != null) {
          if (fi.LastWriteTimeUtc == contents.UtcLastModified)
            return true;
        }
      }
      return false;
    }

    private IEnumerable<FileData> GetCommonFiles(FileDatabase oldState) {
      if (_files.Count == 0 || oldState.Files.Count == 0)
        return Enumerable.Empty<FileData>();

      return oldState.Files.Values.Intersect(_files.Values.Select(x => x.FileData), FileDataComparer.Instance);
    }

    public struct FileInfo {
      private readonly FileData _fileData;
      private readonly bool _isSearchable;

      public FileInfo(FileData fileData, bool isSearchable) {
        _fileData = fileData;
        _isSearchable = isSearchable;
      }

      public FileData FileData { get { return _fileData; } }
      public bool IsSearchable { get { return _isSearchable; } }
    }

    private class FileDataComparer : IEqualityComparer<FileData>, IComparer<FileData> {
      private static readonly FileDataComparer _instance = new FileDataComparer();

      private FileDataComparer() {
      }

      public static FileDataComparer Instance { get { return _instance; } }

      public bool Equals(FileData x, FileData y) {
        return x.FileName.Equals(y.FileName);
      }

      public int GetHashCode(FileData x) {
        return x.FileName.GetHashCode();
      }

      public int Compare(FileData x, FileData y) {
        return x.FileName.CompareTo(y.FileName);
      }
    }
  }
}
