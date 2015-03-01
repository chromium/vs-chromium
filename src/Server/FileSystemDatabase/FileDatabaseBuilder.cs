// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Collections;
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
    /// <summary>
    /// Split large files in chunks of maximum <code>ChunkSize</code> bytes.
    /// </summary>
    private const int ChunkSize = 100 * 1024;
    private readonly IFileSystem _fileSystem;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private Dictionary<FileName, ProjectFileData> _files;
    private Dictionary<DirectoryName, DirectoryData> _directories;
    private Dictionary<FullPath, string> _projectHashes;

    public FileDatabaseBuilder(
      IFileSystem fileSystem,
      IFileContentsFactory fileContentsFactory,
      IProgressTrackerFactory progressTrackerFactory) {
      _fileSystem = fileSystem;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public IFileDatabase Build(IFileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot, FullPathChanges fullPathChanges) {
      using (var logger = new TimeElapsedLogger("Building file database from previous one and file system tree snapshot")) {

        var fileDatabase = (FileDatabase)previousFileDatabase;

        // Compute list of files from tree
        ComputeFileCollection(newSnapshot);

        var unchangedProjects = newSnapshot
          .ProjectRoots.Where(x =>
            fileDatabase.ProjectHashes.ContainsKey(x.Project.RootPath) &&
            fileDatabase.ProjectHashes[x.Project.RootPath] == x.Project.VersionHash)
          .Select(x => x.Project);

        var unchangedProjectSet = new HashSet<IProject>(unchangedProjects, 
          // Use reference equality for IProject is safe, as we keep this
          // dictionary only for the duration of this "Build" call.
          new ReferenceEqualityComparer<IProject>());

        // Don't use file memoization for now, as benefit is dubvious.
        //IFileContentsMemoization fileContentsMemoization = new FileContentsMemoization();
        IFileContentsMemoization fileContentsMemoization = new NullFileContentsMemoization();

        var loadingInfo = new FileContentsLoadingInfo {
          FileContentsMemoization = fileContentsMemoization,
          FullPathChanges = fullPathChanges,
          LoadedCount = 0,
          OldFileDatabase = fileDatabase,
          UnchangedProjects = unchangedProjectSet
        };

        // Merge old state in new state and load all missing files
        LoadFileContents(loadingInfo);

        Logger.LogInfo("{0}{1:n0} unique file contents remaining in memory after memoization of {2:n0} files.",
            logger.Indent,
            fileContentsMemoization.Count,
            _files.Values.Count(x => x.FileData.Contents != null));

        return CreateFileDatabse();
      }
    }

    public IFileDatabase BuildWithChangedFiles(
      IFileDatabase previousFileDatabase,
      IEnumerable<ProjectFileName> changedFiles,
      Action onLoading,
      Action onLoaded) {
      using (new TimeElapsedLogger("Building file database from previous one and list of changed files")) {
        var fileDatabase = (FileDatabase)previousFileDatabase;

        // Update file contents of file data entries of changed files.
        var filesToRead = changedFiles
          .Where(x => x.Project.IsFileSearchable(x.FileName) && fileDatabase.Files.ContainsKey(x.FileName))
          .ToList();

        if (filesToRead.Count == 0)
          return previousFileDatabase;

        // Read file contents.
        onLoading();
        filesToRead.ForAll(x => {
          var newContents = _fileContentsFactory.GetFileContents(x.FileName.FullPath);
          fileDatabase.Files[x.FileName].UpdateContents(newContents);
        });
        onLoaded();

        // Return new file database with updated file contents.
        var filesWithContents = FilterFilesWithContents(fileDatabase.Files.Values);
        return new FileDatabase(
          fileDatabase.ProjectHashes,
          fileDatabase.Files,
          fileDatabase.FileNames,
          fileDatabase.Directories,
          CreateFilePieces(filesWithContents),
          filesWithContents.Count);
      }
    }

    private FileDatabase CreateFileDatabse() {
      using (new TimeElapsedLogger("Freezing FileDatabase state")) {
        var directories = _directories;
        // Note: We cannot use "ReferenceEqualityComparer<FileName>" here because
        // the dictionary will be used in incremental updates where FileName instances
        // may be new instances from a complete file system enumeration.
        var files = new Dictionary<FileName, FileData>(_files.Count);
        var filesWithContentsArray = new FileData[_files.Count];
        int filesWithContentsIndex = 0;
        foreach (var kvp in _files) {
          var fileData = kvp.Value.FileData;
          files.Add(kvp.Key, fileData);
          if (fileData.Contents != null && fileData.Contents.ByteLength > 0) {
            filesWithContentsArray[filesWithContentsIndex++] = fileData;
          }
        }
        var filesWithContents = new ListSegment<FileData>(filesWithContentsArray, 0, filesWithContentsIndex);
        var searchableContentsCollection = CreateFilePieces(filesWithContents);
        LogFileContentsStats(filesWithContents);

        return new FileDatabase(
          _projectHashes,
          files,
          files.Keys.ToArray(),
          directories,
          searchableContentsCollection,
          filesWithContents.Count);
      }
    }

    private static void LogFileContentsStats(IList<FileData> filesWithContents) {
      if (LogStats) {
        var filesByExtensions = filesWithContents
          .GroupBy(x => {
            var ext = Path.GetExtension(x.FileName.FullPath.Value);
            if (ext == "" || x.Contents.ByteLength >= 500 * 1024)
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

    private static List<FileData> FilterFilesWithContents(ICollection<FileData> files) {
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

        var directories = FileSystemSnapshotVisitor.GetDirectories(snapshot);

        var directoryNames = new Dictionary<DirectoryName, DirectoryData>(
          directories.Count,
          // Note: We can use reference equality here because the directory
          // names are contructed unique.
          new ReferenceEqualityComparer<DirectoryName>());

        foreach (var kvp in directories.ToForeachEnum()) {
          directoryNames.Add(
            kvp.Value.DirectoryName,
            new DirectoryData(kvp.Value.DirectoryName, kvp.Value.IsSymLink));
        }

        var files = new Dictionary<FileName,ProjectFileData>(
          directories.Count * 4,
          // Note: We can use reference equality here because the file names are
          // constructed unique and the dictionary will be discarded once we are
          // done building this snapshot.
          new ReferenceEqualityComparer<FileName>());

        foreach (var directory in directories.ToForeachEnum()) {
          foreach (var fileName in directory.Value.ChildFiles.ToForeachEnum()) {
            files.Add(fileName, new ProjectFileData(directory.Key, new FileData(fileName, null)));
          }
        }

        _files = files;
        _directories = directoryNames;
        _projectHashes = snapshot.ProjectRoots.ToDictionary(
          x => x.Project.RootPath,
          x => x.Project.VersionHash);
      }
    }

    private class FileContentsLoadingInfo {
      public FileDatabase OldFileDatabase;
      public FullPathChanges FullPathChanges;
      public IFileContentsMemoization FileContentsMemoization;
      public ISet<IProject> UnchangedProjects;
      public int LoadedCount;
    }

    private void LoadFileContents(FileContentsLoadingInfo loadingInfo) {

      using (var logger = new TimeElapsedLogger("Loading file contents from disk")) {
        using (var progress = _progressTrackerFactory.CreateTracker(_files.Count)) {
          _files.AsParallel().ForAll(fileEntry => {
            Debug.Assert(fileEntry.Value.FileData.Contents == null);

            if (progress.Step()) {
              progress.DisplayProgress(
                (i, n) =>
                  string.Format("Reading file {0:n0} of {1:n0}: {2}", i, n, fileEntry.Value.FileName.FullPath));
            }

            var contents = LoadSingleFileContents(loadingInfo, fileEntry.Value);
            if (contents != null) {
              fileEntry.Value.FileData.UpdateContents(contents);
            }
          });
        }
        Logger.LogInfo("{0}Loaded {1:n0} files", logger.Indent, loadingInfo.LoadedCount);
        Logger.LogMemoryStats(logger.Indent);
      }
    }

    private FileContents LoadSingleFileContents(
      FileContentsLoadingInfo loadingInfo,
      ProjectFileData projectFileData) {

      var fileName = projectFileData.FileName;

      FileData oldFileData;
      if (!loadingInfo.OldFileDatabase.Files.TryGetValue(fileName, out oldFileData)) {
        oldFileData = null;
      }

      // If the file was never loaded before, just load it
      if (oldFileData == null || oldFileData.Contents == null) {
        return LoadSingleFileContentsWorker(loadingInfo, projectFileData);
      }

      bool isSearchable;
      // If the project configuration is unchanged from the previous file
      // database (and the file was present in it), then it is certainly
      // searchable, so no need to make an expensive call to "IsSearchable"
      // again.
      if (loadingInfo.UnchangedProjects.Contains(projectFileData.Project)) {
        isSearchable = true;
      } else {
        // If the file is not searchable in the current project, it should be
        // ignored too. Note that "IsSearachable" is a somewhat expensive
        // operation, as the filename is checked against potentially many glob
        // patterns.
        isSearchable = projectFileData.IsSearchable;
      }

      if (!isSearchable)
        return null;

      // If the file has not changed since the previous snapshot, we can re-use
      // the former file contents snapshot.
      if (IsFileContentsUpToDate(loadingInfo.FullPathChanges, oldFileData)) {
        return oldFileData.Contents;
      }

      return LoadSingleFileContentsWorker(loadingInfo, projectFileData);
    }

    private FileContents LoadSingleFileContentsWorker(
      FileContentsLoadingInfo loadingInfo,
      ProjectFileData projectFileData) {

      // If project configuration has not changed, the file is still not
      // searchable, irrelevant to calling "IsSearchable".
      if (loadingInfo.FullPathChanges != null) {
        if (loadingInfo.FullPathChanges.GetPathChangeKind(projectFileData.FileName.FullPath) == PathChangeKind.None) {
          if (loadingInfo.UnchangedProjects.Contains(projectFileData.Project))
            return null;
        }
      }

      // This is an expensive call, hopefully avoided by the code above.
      if (!projectFileData.IsSearchable)
        return null;

      var fileContents = _fileContentsFactory.GetFileContents(projectFileData.FileName.FullPath);
      if (!(fileContents is BinaryFileContents)) {
        Interlocked.Increment(ref loadingInfo.LoadedCount);
      }
      return loadingInfo.FileContentsMemoization.Get(projectFileData.FileName, fileContents);
    }

    private bool IsFileContentsUpToDate(FullPathChanges fullPathChanges, FileData oldFileData) {
      Debug.Assert(oldFileData.Contents != null);

      var fullPath = oldFileData.FileName.FullPath;

      if (fullPathChanges != null) {
        // We don't get file change events for file in symlinks, so we can't
        // rely on fullPathChanges contents for our heuristic of avoiding file
        // system access.
        if (!FileDatabase.IsContainedInSymLinkHelper(_directories, oldFileData.FileName)) {
          return fullPathChanges.GetPathChangeKind(fullPath) == PathChangeKind.None;
        }
      }

      // Do the "expensive" check by going to the file system.
      var fi = _fileSystem.GetFileInfoSnapshot(fullPath);
      return
        (fi.Exists) &&
        (fi.IsFile) &&
        (fi.LastWriteTimeUtc == oldFileData.Contents.UtcLastModified);
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
