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
      return ComputeFileDatabase((FileDatabase)previousFileDatabase, newSnapshot);
    }

    public IFileDatabase BuildWithChangedFiles(IFileDatabase previousFileDatabase, IEnumerable<Tuple<IProject, FileName>> changedFiles) {
      Logger.Log("BuildWithChangedFiles.");
      var sw = Stopwatch.StartNew();

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
      var result = new FileDatabase(
          fileDatabase.Files,
          fileDatabase.Directories,
          CreateSearchableContentsCollection(fileDatabase.Files.Values));

      sw.Stop();
      Logger.Log("Done BuildWithChangedFiles in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();

      return result;
    }

    /// <summary>
    /// Prepares this instance for searches by computing various snapshots from
    /// the previous <see cref="FileDatabase"/> snapshot and the new current
    /// <see cref="FileSystemTreeSnapshot"/> instance.
    /// </summary>
    private FileDatabase ComputeFileDatabase(FileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot) {
      if (previousFileDatabase == null)
        throw new ArgumentNullException("previousFileDatabase");

      if (newSnapshot == null)
        throw new ArgumentNullException("newSnapshot");

      // Compute list of files from tree
      ComputeFileCollection(newSnapshot);

      // Merge old state in new state
      var fileContentsMemoization = new FileContentsMemoization();
      TransferUnchangedFileContents(previousFileDatabase, fileContentsMemoization);

      // Load file contents into newState
      ReadMissingFileContents(fileContentsMemoization);

      Logger.Log("{0:n0} unique file contents remaining in memory after memoization of {1:n0} files.", 
        fileContentsMemoization.Count, _files.Values.Count(x => x.FileData.Contents != null));

      return CreateFileDatabse();
    }

    private FileDatabase CreateFileDatabse() {
      Logger.Log("Freezing FileDatabase state.");
      var sw = Stopwatch.StartNew();

      var files = _files.ToDictionary(x => x.Key, x => x.Value.FileData);
      var directories = _directories;
      var searchableContentsCollection = CreateSearchableContentsCollection(files.Values);

      sw.Stop();
      Logger.Log("Done freezing FileDatabase state in {0:n0} msec.", sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();

      return new FileDatabase(files, directories, searchableContentsCollection);
    }

    private static IList<ISearchableContents> CreateSearchableContentsCollection(IEnumerable<FileData> files) {
      int id = 0;
      Func<int> fileIdFactory = () => id++;
      // Note: Partitioning evenly ensures that each processor used by PLinq
      // will deal with a partition of equal "weight". In this case, we make
      // sure each partition contains not only the same amount of files, but
      // also (as close to as possible) the same amount of "bytes". For example,
      // if we have 100 files totaling 32MB and 4 processors, we will end up
      // with 4 partitions of (exactly) 25 files totalling (approximately) 8MB
      // each.
      var searchableContentsCollection = files
        .Where(x => x.Contents != null)
        .SelectMany(x => SplitFileContents(x, fileIdFactory()))
        .ToList()
        .PartitionEvenly(x => x.ByteLength)
        // Sort to that smaller entries are at the beginnig of the partitions so
        // that regex searches hitting more than max. results don't spend most
        // of their time searching in large file chunks which have little chance
        // of containing hits -- because large files tend to contain useless
        // data from a search perspective.
        .Select(x => x.OrderBy(y => y.ByteLength))
        .SelectMany(x => x)
        .Cast<ISearchableContents>()
        .ToArray();
      return searchableContentsCollection;
    }

    private static IEnumerable<SearchableContents> SplitFileContents(FileData fileData, int fileId) {
      var chunkOffset = 0L;
      var totalLength = fileData.Contents.ByteLength;
      while (totalLength > 0) {
        var chunkLength = Math.Min(totalLength, 100 * 1024);
        yield return new SearchableContents(fileData, fileId, chunkOffset, chunkLength);

        totalLength -= chunkLength;
        chunkOffset += chunkLength;
      }
    }

    private void ComputeFileCollection(FileSystemTreeSnapshot snapshot) {
      Logger.Log("Computing list of searchable files from FileSystemTree.");
      var ssw = new MultiStepStopWatch();

      var directories = FileSystemSnapshotVisitor.GetDirectories(snapshot).ToList();
      ssw.Step(sw => Logger.Log("Done flattening file system tree snapshot in {0:n0} msec.", sw.ElapsedMilliseconds));

      var directoryNames = directories
        .ToDictionary(x => x.Value.DirectoryName, x => x.Value.DirectoryData);
      ssw.Step(sw => Logger.Log("Done creating array of directory names in {0:n0} msec.", sw.ElapsedMilliseconds));

      var searchableFiles = directories
        .AsParallel()
        .SelectMany(x => x.Value.ChildFiles.Select(y => KeyValuePair.Create(x.Key, y)))
        .Select(x => new FileInfo(new FileData(x.Value, null), x.Key.IsFileSearchable(x.Value)))
        .ToList();
      ssw.Step(sw => Logger.Log("Done creating list of files in {0:n0} msec.", sw.ElapsedMilliseconds));

      var files = searchableFiles
        .ToDictionary(x => x.FileData.FileName, x => x);
      ssw.Step(sw => Logger.Log("Done creating dictionary of files in {0:n0} msec.", sw.ElapsedMilliseconds));

      Logger.Log("Done computing list of searchable files from FileSystemTree.");
      Logger.LogMemoryStats();

      _files = new FileNameDictionary<FileInfo>(files);
      _directories = directoryNames;
    }

    private void TransferUnchangedFileContents(FileDatabase oldState, IFileContentsMemoization fileContentsMemoization) {
      Logger.Log("Checking for out of date files.");
      var sw = Stopwatch.StartNew();

      IList<FileData> commonOldFiles = GetCommonFiles(oldState).ToArray();
      using (var progress = _progressTrackerFactory.CreateTracker(commonOldFiles.Count)) {
        commonOldFiles
          .AsParallel()
          .Where(oldFileData => {
            if (progress.Step()) {
              progress.DisplayProgress((i, n) => string.Format("Checking file timestamp {0:n0} of {1:n0}: {2}", i, n, oldFileData.FileName.FullPath));
            }
            return IsFileContentsUpToDate(oldFileData);
          })
          .ForAll(oldFileData => {
            var contents = fileContentsMemoization.Get(oldFileData.FileName, oldFileData.Contents);
            _files[oldFileData.FileName].FileData.UpdateContents(contents);
          });
      }

      Logger.Log("Done checking for {0:n0} out of date files in {1:n0} msec.", commonOldFiles.Count,
                 sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
    }

    /// <summary>
    /// Reads the content of all file entries that have no content (yet). Returns the # of files read from disk.
    /// </summary>
    private void ReadMissingFileContents(IFileContentsMemoization fileContentsMemoization) {
      Logger.Log("Loading file contents from disk.");
      var sw = Stopwatch.StartNew();

      using (var progress = _progressTrackerFactory.CreateTracker(_files.Count)) {
        _files.Values
          .AsParallel()
          .ForAll(fileInfo => {
            if (progress.Step()) {
              progress.DisplayProgress((i, n) => string.Format("Reading file {0:n0} of {1:n0}: {2}", i, n, fileInfo.FileData.FileName.FullPath));
            }
            if (fileInfo.IsSearchable && fileInfo.FileData.Contents == null) {
              var fileContents = _fileContentsFactory.GetFileContents(fileInfo.FileData.FileName.FullPath);
              fileInfo.FileData.UpdateContents(fileContentsMemoization.Get(fileInfo.FileData.FileName, fileContents));
            }
          });
      }

      sw.Stop();
      Logger.Log("Done loading file contents from disk: loaded {0:n0} files in {1:n0} msec.",
        _files.Count,
        sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();
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
