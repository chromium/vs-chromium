// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase {
  public class FileDatabaseBuilder {
    private static bool LogStats = false;
    private const int ChunkSize = 100 * 1024;
    private readonly IFileSystem _fileSystem;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private Dictionary<FileName, ProjectFileData> _files;
    private Dictionary<DirectoryName, DirectoryData> _directories;

    public FileDatabaseBuilder(
      IFileSystem fileSystem,
      IFileContentsFactory fileContentsFactory,
      IProgressTrackerFactory progressTrackerFactory) {
      _fileSystem = fileSystem;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public IFileDatabase Build(IFileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot) {
      using (var logger = new TimeElapsedLogger("Building file database from previous one and file system tree snapshot")) {
        var fileDatabase = (FileDatabase)previousFileDatabase;
        // Compute list of files from tree
        ComputeFileCollection(newSnapshot);

        // Don't use file memoization for now, as benefit is dubvious.
        //IFileContentsMemoization fileContentsMemoization = new FileContentsMemoization();
        IFileContentsMemoization fileContentsMemoization = new NullFileContentsMemoization();

        // Merge old state in new state
        TransferUnchangedFileContents(fileDatabase, fileContentsMemoization);

        // Load file contents into newState
        ReadMissingFileContents(fileContentsMemoization);

        Logger.LogInfo("{0}{1:n0} unique file contents remaining in memory after memoization of {2:n0} files.",
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
          fileDatabase.FileNames,
          fileDatabase.Directories,
          fileDatabase.DirectoryNames,
          CreateFilePieces(filesWithContents),
          filesWithContents.Count);
      }
    }

    private FileDatabase CreateFileDatabse() {
      using (new TimeElapsedLogger("Freezing FileDatabase state")) {
        // Note: We cannot use "ReferenceEqualityComparer<FileName>" here because
        // the dictionary will be used in incremental updates where FileName instances
        // may be new instances from a complete file system enumeration.
        var files = _files.ToDictionary(x => x.Key, x => x.Value.FileData);
        var directories = _directories;
        var filesWithContents = GetFilesWithContents(files.Values);
        var searchableContentsCollection = CreateFilePieces(filesWithContents);
        if (LogStats) {
          var filesByExtensions = filesWithContents
            .GroupBy(x => {
              var ext = Path.GetExtension(x.FileName.FullPath.Value);
              if (ext == "" || x.Contents.ByteLength >=  500 * 1024)
                return Path.GetFileName(x.FileName.FullPath.Value);
              return ext;
            })
            .Select(g => {
            var count = g.Count();
            var size = g.Aggregate(0L, (c, x) => c + x.Contents.ByteLength);
              return Tuple.Create(g.Key, count, size);
            })
            .OrderByDescending(x => x.Item3);
          filesByExtensions.ForAll(g => {
            var count = g.Item2;
            var size = g.Item3;
            if (size > 1024L) {
              Logger.LogInfo("{0}: {1:n0} files, {2:n0} bytes", g.Item1, count, size);
            }
          });
        }
        return new FileDatabase(
          files,
          files.Keys.ToArray(),
          directories,
          directories.Keys.ToArray(),
          searchableContentsCollection,
          filesWithContents.Count);
      }
    }

    /// <summary>
    /// Partitioning evenly ensures that each processor used by PLinq will deal
    /// with a partition of equal "weight". In this case, we make sure each
    /// partition contains not only the same amount of files, but also (as close
    /// to as possible) the same amount of "bytes". For example, if we have 100
    /// files totaling 32MB and 4 processors, we will end up with 4 partitions
    /// of (exactly) 25 files totalling (approximately) 8MB each.
    /// 
    /// Note: This code inside this method is not the cleanest, but it is
    /// written in a way that tries to minimiz the # of large array allocations.
    /// </summary>
    private static IList<IFileContentsPiece> CreateFilePieces(ICollection<FileData> filesWithContents) {
      // Factory for file identifiers
      int currentFileId = 0;
      Func<int> fileIdFactory = () => currentFileId++;

      // Predicate to figure out if a file is "small"
      Func<FileData, bool> isSmallFile = x => x.Contents.ByteLength <= ChunkSize;

      // Count the total # of small and large files, while splitting large files
      // into their fragments.
      var smallFilesCount = 0;
      var largeFiles = new List<FileContentsPiece>(filesWithContents.Count / 100);
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
      var fileContents = new FileContentsPiece[totalFileCount];
      var partitionCount = Environment.ProcessorCount;
      var generator = new PartitionIndicesGenerator(
        totalFileCount,
        partitionCount);

      // Store large files
      foreach (var item in largeFiles) {
        fileContents[generator.Next()] = item;
      }
      // Store small files
      foreach (var fileData in filesWithContents) {
        if (isSmallFile(fileData)) {
          var item = fileData.Contents.CreatePiece(
            fileData.FileName,
            fileIdFactory(),
            fileData.Contents.TextRange);
          fileContents[generator.Next()] = item;
        }
      }

#if false
      Debug.Assert(fileContents.All(x => x != null));
      Debug.Assert(fileContents.Aggregate(0L, (c, x) => c + x.ByteLength) ==
        filesWithContents.Aggregate(0L, (c, x) => c + x.Contents.ByteLength));
      fileContents.GetPartitionRanges(partitionCount).ForAll(
        (index, range) => {
          Logger.LogInfo("Partition {0} has a weight of {1:n0}",
            index,
            fileContents
              .Skip(range.Key)
              .Take(range.Value)
              .Aggregate(0L, (c, x) => c + x.ByteLength));
        });
#endif
      return fileContents;
    }

    private static List<FileData> GetFilesWithContents(ICollection<FileData> files) {
      // Create filesWithContents with minimum memory allocations and copying.
      var filesWithContents = new List<FileData>(files.Count);
      filesWithContents.AddRange(files.Where(x => x.Contents != null && x.Contents.ByteLength > 0));
      return filesWithContents;
    }

    /// <summary>
    ///  Create chunks of 100KB for files larger than 100KB.
    /// </summary>
    private static IEnumerable<FileContentsPiece> SplitFileContents(FileData fileData, int fileId) {
      var range = fileData.Contents.TextRange;
      while (range.Length > 0) {
        // TODO(rpaquay): Be smarter and split around new lines characters.
        var chunkLength = Math.Min(range.Length, ChunkSize);
        var chunk = new TextRange(range.Position, chunkLength);
        yield return fileData.Contents.CreatePiece(fileData.FileName, fileId, chunk);

        range = new TextRange(chunk.EndPosition, range.EndPosition - chunk.EndPosition);
      }
    }

    private void ComputeFileCollection(FileSystemTreeSnapshot snapshot) {
      using (new TimeElapsedLogger("Computing tables of directory names and file names from FileSystemTree")) {
        var directories = FileSystemSnapshotVisitor.GetDirectories(snapshot).ToList();

        var directoryNames = directories
          // Note: We can use reference equality here because the directory
          // names are contructed unique.
          .ToDictionary(
            x => x.Value.DirectoryName,
            x => x.Value.DirectoryData,
            new ReferenceEqualityComparer<DirectoryName>());

        var files = directories
          .AsParallel()
          .SelectMany(x => x.Value.ChildFiles.Select(y => new ProjectFileData(x.Key, new FileData(y, null))))
          // Note: We can use reference equality here because the file names are
          // constructed unique and the dictionary will be discarded once we are
          // done building this snapshot.
          .ToDictionary(
            x => x.FileName,
            new ReferenceEqualityComparer<FileName>());

        _files = files;
        _directories = directoryNames;
      }
    }

    private void TransferUnchangedFileContents(FileDatabase oldState, IFileContentsMemoization fileContentsMemoization) {
      using (new TimeElapsedLogger("Checking for out of date files")) {
        IList<KeyValuePair<FileData, ProjectFileData>> commonOldFiles = GetCommonFiles(oldState).ToArray();
        using (var progress = _progressTrackerFactory.CreateTracker(commonOldFiles.Count)) {
          var commonSearchableFiles = commonOldFiles
            .AsParallel()
            .Where(kvp => {
              var oldFileData = kvp.Key;
              var projectFileData = kvp.Value;
              if (progress.Step()) {
                progress.DisplayProgress(
                  (i, n) =>
                    string.Format("Checking file timestamp {0:n0} of {1:n0}: {2}", i, n, oldFileData.FileName.FullPath));
              }
              // If file was not previously searchable, it is certainly not a
              // "common searchable" file.
              if (oldFileData.Contents == null)
                return false;

              // If the file is not searchable in the current project, it should
              // be ignored too. Note that "IsSearachable" is a somewhat
              // expensive operation, as the filename is checked against
              // potentially many glob patterns.
              if (!projectFileData.IsSearchable)
                return false;

              // If the file has changed since the previous snapshot, it should
              // be ignored too.
              return IsFileContentsUpToDate(oldFileData);
            });

          commonSearchableFiles.ForAll(kvp => {
            var oldFileData = kvp.Key;
            var projectFileData = kvp.Value;

            Debug.Assert(oldFileData.Contents != null);

            var contents = fileContentsMemoization.Get(projectFileData.FileName, oldFileData.Contents);
            Debug.Assert(contents != null);

            Debug.Assert(projectFileData.FileData.Contents == null);
            projectFileData.FileData.UpdateContents(contents);
          });
        }
      }
    }

    /// <summary>
    /// Reads the content of all file entries that have no content (yet). Returns the # of files read from disk.
    /// </summary>
    private void ReadMissingFileContents(IFileContentsMemoization fileContentsMemoization) {
      using (var logger = new TimeElapsedLogger("Loading file contents from disk")) {
        int loadedFileCount = 0;
        using (var progress = _progressTrackerFactory.CreateTracker(_files.Count)) {
          _files.Values
            .AsParallel()
            .ForAll(projectFileData => {
              if (progress.Step()) {
                progress.DisplayProgress(
                  (i, n) =>
                    string.Format("Reading file {0:n0} of {1:n0}: {2}", i, n, projectFileData.FileName.FullPath));
              }
              // Load the file only if 1) it has no contents yet (from a
              // previous snapshot) and 2) it is searchable
              if (projectFileData.FileData.Contents == null && projectFileData.IsSearchable) {
                var fileContents = _fileContentsFactory.GetFileContents(projectFileData.FileName.FullPath);
                if (!(fileContents is BinaryFileContents)) {
                  Interlocked.Increment(ref loadedFileCount);
                }
                fileContents = fileContentsMemoization.Get(projectFileData.FileName, fileContents);
                projectFileData.FileData.UpdateContents(fileContents);
              }
            });
        }

        Logger.LogInfo("{0}Loaded {1:n0} files", logger.Indent, loadedFileCount);
        Logger.LogMemoryStats(logger.Indent);
      }
    }

    private bool IsFileContentsUpToDate(FileData oldFileData) {
      Debug.Assert(oldFileData.Contents != null);
      // TODO(rpaquay): The following File.Exists and File.GetLastWriteTimUtc
      // are expensive operations. Given we have FileSystemChanged events when
      // files change on disk, we could be smarter here and avoid 99% of these
      // checks in common cases.
      // Note that this could be tricky, because we don't get file change events
      // for files that are contained in symbolic link directories for example.
      // This means we would have to be absolutely sure about what happend to
      // the chain of parent directories before being able to make a "smarter"
      // decision on when to check the filesystem (or skip the check).
      var fi = _fileSystem.GetFileInfoSnapshot(oldFileData.FileName.FullPath);
      return
        (fi.Exists) &&
        (fi.LastWriteTimeUtc == oldFileData.Contents.UtcLastModified);
    }

    private IEnumerable<KeyValuePair<FileData, ProjectFileData>> GetCommonFiles(FileDatabase oldState) {
      if (_files.Count == 0 || oldState.Files.Count == 0)
        yield break;

      foreach (var newFile in _files) {
        FileData oldFileData;
        if (oldState.Files.TryGetValue(newFile.Key, out oldFileData)) {
          yield return KeyValuePair.Create(oldFileData, newFile.Value);
        }
      }
    }

    /// <summary>
    /// Utility class to store a FileData instance along with the IProject
    /// instance the file is coming from. This is needed because an IProject
    /// instance is needed during snapshot computation to determine if a file is
    /// searchable.
    /// </summary>
    private struct ProjectFileData {
      private readonly IProject _project;
      private readonly FileData _fileData;

      public ProjectFileData(IProject project, FileData fileData) {
        _project = project;
        _fileData = fileData;
      }

      public IProject Project { get { return _project; } }
      public FileData FileData { get { return _fileData; } }
      public FileName FileName { get { return _fileData.FileName; } }
      public bool IsSearchable {
        get {
          return _project.IsFileSearchable(_fileData.FileName);
        }
      }
    }

    /// <summary>
    /// Implementation of IEqualityComparer where object references is the
    /// identity.
    /// </summary>
    private class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class {
      public bool Equals(T x, T y) {
        return object.ReferenceEquals(x, y);
      }

      public int GetHashCode(T obj) {
        return RuntimeHelpers.GetHashCode(obj);
      }
    }

    /// <summary>
    /// Utility class to generate the sequence of indices into an array
    /// logically divided into a given number of partitions of identical size.
    /// </summary>
    private class PartitionIndicesGenerator {
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
  }
}
