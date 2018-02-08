// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase.Builder {
  public class FileDatabaseBuilder {
    /// <summary>
    /// Split large files in chunks of maximum <code>ChunkSize</code> bytes.
    /// </summary>
    private const int ChunkSize = 100 * 1024;

    private readonly IFileSystem _fileSystem;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;

    public FileDatabaseBuilder(
      IFileSystem fileSystem,
      IFileContentsFactory fileContentsFactory,
      IProgressTrackerFactory progressTrackerFactory) {
      _fileSystem = fileSystem;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public IFileDatabaseSnapshot Build(IFileDatabaseSnapshot previousDatabase, FileSystemSnapshot newSnapshot, FullPathChanges fullPathChanges,
      Action onLoading, Action onLoaded, Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken) {
      using (new TimeElapsedLogger("Building file database from previous one and file system tree snapshot", cancellationToken, InfoLogger.Instance)) {
        using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
          onLoading();
          progress.DisplayProgress((i, n) => "Preparing list of files to load from disk");

          var fileDatabase = (FileDatabaseSnapshot)previousDatabase;

          // Compute list of files from tree
          var entities = ComputeFileSystemEntities(newSnapshot, cancellationToken);

          cancellationToken.ThrowIfCancellationRequested();
          var unchangedProjects = newSnapshot
            .ProjectRoots.Where(x =>
              fileDatabase.ProjectHashes.ContainsKey(x.Project.RootPath) &&
              fileDatabase.ProjectHashes[x.Project.RootPath] == x.Project.VersionHash)
            .Select(x => x.Project);

          cancellationToken.ThrowIfCancellationRequested();
          var unchangedProjectSet = new HashSet<IProject>(unchangedProjects,
            // Use reference equality for IProject is safe, as we keep this
            // dictionary only for the duration of this "Build" call.
            new ReferenceEqualityComparer<IProject>());

          cancellationToken.ThrowIfCancellationRequested();
          var loadingContext = new FileContentsLoadingContext {
            FullPathChanges = fullPathChanges,
            LoadedTextFileCount = 0,
            OldFileDatabaseSnapshot = fileDatabase,
            UnchangedProjects = unchangedProjectSet,
            PartialProgressReporter = new PartialProgressReporter(
              TimeSpan.FromSeconds(5.0),
              () => {
                Logger.LogInfo("Creating intermedidate file database for partial progress reporting");
                var database = CreateFileDatabase(entities, false, cancellationToken);
                onIntermadiateResult(database);
              })
          };

          // Merge old state in new state and load all missing files
          LoadFileContents(entities, loadingContext, cancellationToken);

          var result = CreateFileDatabase(entities, true, cancellationToken);
          onLoaded();
          return result;
        }
      }
    }

    public IFileDatabaseSnapshot BuildWithChangedFiles(IFileDatabaseSnapshot previousFileDatabaseSnapshot,
      FileSystemSnapshot fileSystemSnapshot, IEnumerable<ProjectFileName> changedFiles,
      Action onLoading, Action onLoaded, Action<IFileDatabaseSnapshot> onIntermadiateResult,
      CancellationToken cancellationToken) {

      using (new TimeElapsedLogger("Building file database from previous one and list of changed files", cancellationToken, InfoLogger.Instance)) {
        Invariants.Assert(previousFileDatabaseSnapshot is FileDatabaseSnapshot);
        var previousFileDatabase = (FileDatabaseSnapshot)previousFileDatabaseSnapshot;

        // Update file contents of file data entries of changed files.
        var filesToRead = changedFiles
          .Where(x => x.Project.IsFileSearchable(x.FileName) && previousFileDatabase.Files.ContainsKey(x.FileName))
          .ToList();

        if (filesToRead.Count == 0) {
          Logger.LogInfo("None of the changed file is searchable, return previous database snapshot");
          return previousFileDatabaseSnapshot;
        }

        // Read file contents.
        onLoading();
        filesToRead.ForAll(x => {
          var newContents = _fileContentsFactory.ReadFileContents(x.FileName.FullPath);
          DangerousUpdateFileTableEntry(previousFileDatabase, x.FileName, newContents);
        });
        onLoaded();

        // Return new file database with updated file contents.
        return new FileDatabaseSnapshot(
          previousFileDatabase.ProjectHashes,
          previousFileDatabase.Directories,
          previousFileDatabase.Files);
      }
    }

    /// <summary>
    /// Note: Concurrency: There are potentially many readers (search threads), but
    /// only one writer (us). Updating the hash table with the line below assumes
    /// that 1) the entry exists and 2) the hash table implementation does not
    /// mutate any of its internal state when updating an existing entry.
    /// <para>
    /// 1) is verified because we the "filesToRead" collections only contains
    ///    file names contained in this table.</para>
    /// <para>
    /// 2) is verified because the two possible implementations, <see cref="Dictionary{TKey,TValue}"/>
    ///    and <see cref="SlimHashTable{TKey,TValue}"/> behave that way.</para>
    /// </summary>
    private static void DangerousUpdateFileTableEntry(FileDatabaseSnapshot fileDatabase, FileName fileName, FileContents newContents) {
      fileDatabase.Files[fileName] = new FileWithContents(fileName, newContents);
    }

    private FileDatabaseSnapshot CreateFileDatabase(FileSystemEntities entities, bool notifyProgress, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      using (new TimeElapsedLogger("Freezing file database state", cancellationToken)) {
        var progress = notifyProgress ? _progressTrackerFactory.CreateIndeterminateTracker() : null;
        try {
          progress?.DisplayProgress((i, n) => "Finalizing index update");

          var directories = entities.Directories;
          // Note: We cannot use "ReferenceEqualityComparer<FileName>" here because
          // the dictionary will be used in incremental updates where FileName instances
          // may be new instances from a complete file system enumeration.
          var files = new SlimHashTable<FileName, FileWithContents>(v => v.FileName, entities.Files.Count);
          foreach (var kvp in entities.Files) {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(kvp.Key, new FileWithContents(kvp.Value.FileName, kvp.Value.Contents));
          }

          return new FileDatabaseSnapshot(entities.ProjectHashes, directories, files);
        } finally {
          progress?.Dispose();
        }
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
    public static IList<FileContentsPiece> CreateFilePieces(ICollection<FileWithContents> files) {
      var filesWithContents = FilterFilesWithContents(files);

      // Factory for file identifiers
      var currentFileId = 0;
      Func<int> fileIdFactory = () => currentFileId++;

      // Predicate to figure out if a file is "small"
      Func<FileWithContents, bool> isSmallFile = x => x.Contents.ByteLength <= ChunkSize;

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
      var filePieces = new FileContentsPiece[totalFileCount];
      var partitionCount = Environment.ProcessorCount;
      var generator = new PartitionIndicesGenerator(totalFileCount, partitionCount);

      // Store large files
      foreach (var item in largeFiles) {
        filePieces[generator.Next()] = item;
      }

      // Store small files
      foreach (var fileData in filesWithContents) {
        if (isSmallFile(fileData)) {
          var item = fileData.Contents.CreatePiece(
            fileData.FileName,
            fileIdFactory(),
            fileData.Contents.TextRange);
          filePieces[generator.Next()] = item;
        }
      }

      FileDatabaseDebugLogger.LogFilePieces(filesWithContents, filePieces, partitionCount);
      return filePieces;
    }

    private static List<FileWithContents> FilterFilesWithContents(ICollection<FileWithContents> files) {
      // Create filesWithContents with minimum memory allocations and copying.
      var filesWithContents = new List<FileWithContents>(files.Count / 4);
      filesWithContents.AddRange(files.Where(FileHasContents));
      return filesWithContents;
    }

    public static bool FileHasContents(FileWithContents x) {
      return x.HasContents();
    }

    /// <summary>
    ///  Create chunks of 100KB for files larger than 100KB.
    /// </summary>
    private static IEnumerable<FileContentsPiece> SplitFileContents(FileWithContents fileWithContents, int fileId) {
      var range = fileWithContents.Contents.TextRange;
      while (range.Length > 0) {
        // TODO(rpaquay): Be smarter and split around new lines characters.
        var chunkLength = Math.Min(range.Length, ChunkSize);
        var chunk = new TextRange(range.Position, chunkLength);
        yield return fileWithContents.Contents.CreatePiece(fileWithContents.FileName, fileId, chunk);

        range = new TextRange(chunk.EndPosition, range.EndPosition - chunk.EndPosition);
      }
    }

    private class FileSystemEntities {
      public IDictionary<FileName, ProjectFileData> Files { get; set; }
      public IDictionary<DirectoryName, DirectoryData> Directories { get; set; }
      public IDictionary<FullPath, string> ProjectHashes { get; set; }
    }

    private FileSystemEntities ComputeFileSystemEntities(FileSystemSnapshot snapshot, CancellationToken cancellationToken) {
      using (new TimeElapsedLogger("Computing tables of directory names and file names from FileSystemTree", cancellationToken)) {

        var directories = FileSystemSnapshotVisitor.GetDirectories(snapshot);

        //var directoryNames = new Dictionary<DirectoryName, DirectoryData>(
        var directoryNames = new SlimHashTable<DirectoryName, DirectoryData>(
          v => v.DirectoryName,
          directories.Count,
          // Note: We can use reference equality here because the directory
          // names are contructed unique.
          new ReferenceEqualityComparer<DirectoryName>());

        foreach (var kvp in directories.ToForeachEnum()) {
          directoryNames.Add(
            kvp.Value.DirectoryName,
            new DirectoryData(kvp.Value.DirectoryName, kvp.Value.IsSymLink));
        }

        var files = new SlimHashTable<FileName, ProjectFileData>(
          v => v.FileName,
          directories.Count * 2,
          // Note: We can use reference equality here because the file names are
          // constructed unique and the dictionary will be discarded once we are
          // done building this snapshot.
          new FileNameReferenceEqualityComparer());

        foreach (var directory in directories.ToForeachEnum()) {
          foreach (var fileName in directory.Value.ChildFiles.ToForeachEnum()) {
            files.Add(fileName, new ProjectFileData(directory.Key, fileName, null));
          }
        }

        return new FileSystemEntities {
          Files = files,
          Directories = directoryNames,
          ProjectHashes = snapshot.ProjectRoots.ToDictionary(x => x.Project.RootPath, x => x.Project.VersionHash)
        };
      }
    }

    private class FileNameReferenceEqualityComparer : IEqualityComparer<FileName> {
      public bool Equals(FileName x, FileName y) {
        return ReferenceEquals(x.Parent, y.Parent) &&
               ReferenceEquals(x.Name, y.Name);
      }

      public int GetHashCode(FileName obj) {
        return HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Parent), RuntimeHelpers.GetHashCode(obj.Name));
      }
    }

    private class FileContentsLoadingContext {
      public FileDatabaseSnapshot OldFileDatabaseSnapshot;
      public FullPathChanges FullPathChanges;
      public ISet<IProject> UnchangedProjects;
      public int LoadedTextFileCount;
      public int LoadedBinaryFileCount;
      public PartialProgressReporter PartialProgressReporter;
    }

    private void LoadFileContents(FileSystemEntities entities, FileContentsLoadingContext loadingContext, CancellationToken cancellationToken) {
      using (new TimeElapsedLogger("Loading file contents from disk", cancellationToken)) {
        using (var progress = _progressTrackerFactory.CreateTracker(entities.Files.Count)) {
          entities.Files.AsParallelWrapper().ForAll(fileEntry => {
            Invariants.Assert(fileEntry.Value.Contents == null);

            // ReSharper disable once AccessToDisposedClosure
            if (progress.Step()) {
              // ReSharper disable once AccessToDisposedClosure
              progress.DisplayProgress((i, n) =>
                string.Format("Reading file {0:n0} of {1:n0}: {2}", i, n, fileEntry.Value.FileName.FullPath));

              // Check for cancellation
              if (cancellationToken.IsCancellationRequested) {
                loadingContext.PartialProgressReporter.ReportProgressNow();
                cancellationToken.ThrowIfCancellationRequested();
              }
            }

            var contents = LoadSingleFileContents(entities, loadingContext, fileEntry.Value);
            if (contents != null) {
              entities.Files[fileEntry.Key] = fileEntry.Value.WithContents(contents);
            }
          });
        }
      }
      Logger.LogInfo("Loaded {0:n0} text files from disk, skipped {1:n0} binary files.",
        loadingContext.LoadedTextFileCount,
        loadingContext.LoadedBinaryFileCount);
    }

    private FileContents LoadSingleFileContents(
      FileSystemEntities entities,
      FileContentsLoadingContext loadingContext,
      ProjectFileData projectFileData) {

      var fileName = projectFileData.FileName;
      var oldFileData = loadingContext.OldFileDatabaseSnapshot.Files.GetValueType(fileName);

      // If the file was never loaded before, just load it
      if (oldFileData == null || oldFileData.Value.Contents == null) {
        return LoadSingleFileContentsWorker(loadingContext, projectFileData);
      }

      bool isSearchable;
      // If the project configuration is unchanged from the previous file
      // database (and the file was present in it), then it is certainly
      // searchable, so no need to make an expensive call to "IsSearchable"
      // again.
      if (loadingContext.UnchangedProjects.Contains(projectFileData.Project)) {
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
      if (IsFileContentsUpToDate(entities, loadingContext.FullPathChanges, oldFileData.Value)) {
        return oldFileData.Value.Contents;
      }

      return LoadSingleFileContentsWorker(loadingContext, projectFileData);
    }

    private FileContents LoadSingleFileContentsWorker(
      FileContentsLoadingContext loadingContext,
      ProjectFileData projectFileData) {

      // If project configuration has not changed, the file is still not
      // searchable, irrelevant to calling "IsSearchable".
      if (loadingContext.FullPathChanges != null) {
        if (loadingContext.FullPathChanges.ShouldSkipLoadFileContents(projectFileData.FileName.FullPath)) {
          if (loadingContext.UnchangedProjects.Contains(projectFileData.Project))
            return null;
        }
      }

      // This is an expensive call, hopefully avoided by the code above.
      if (!projectFileData.IsSearchable)
        return null;

      loadingContext.PartialProgressReporter.ReportProgress();

      var fileContents = _fileContentsFactory.ReadFileContents(projectFileData.FileName.FullPath);
      if (fileContents is BinaryFileContents) {
        Interlocked.Increment(ref loadingContext.LoadedBinaryFileCount);
      } else {
        Interlocked.Increment(ref loadingContext.LoadedTextFileCount);
      }
      return fileContents;
    }

    private bool IsFileContentsUpToDate(FileSystemEntities entities, FullPathChanges fullPathChanges, FileWithContents existingFileWithContents) {
      Invariants.Assert(existingFileWithContents.Contents != null);

      var fullPath = existingFileWithContents.FileName.FullPath;

      if (fullPathChanges != null) {
        // We don't get file change events for file in symlinks, so we can't
        // rely on fullPathChanges contents for our heuristic of avoiding file
        // system access.
        if (!FileDatabaseSnapshot.IsContainedInSymLinkHelper(entities.Directories, existingFileWithContents.FileName)) {
          return fullPathChanges.ShouldSkipLoadFileContents(fullPath);
        }
      }

      // Do the "expensive" check by going to the file system.
      var fi = _fileSystem.GetFileInfoSnapshot(fullPath);
      return
        (fi.Exists) &&
        (fi.IsFile) &&
        (fi.LastWriteTimeUtc == existingFileWithContents.Contents.UtcLastModified);
    }

    /// <summary>
    /// Utility class to store a FileWithContents instance along with the IProject
    /// instance the file is coming from. This is needed because an IProject
    /// instance is needed during snapshot computation to determine if a file is
    /// searchable.
    /// </summary>
    private struct ProjectFileData {
      private readonly IProject _project;
      private readonly FileName _fileName;
      private readonly FileContents _contents;

      public ProjectFileData(IProject project, FileName fileName, FileContents contents) {
        _project = project;
        _fileName = fileName;
        _contents = contents;
      }

      public IProject Project => _project;

      /// <summary>
      /// The file name. Note the file may not exist on disk anymore, or the file
      /// maybe not be indexed. Use FileContent to look for the snapshot of the
      /// file contents at index creation.
      /// </summary>
      public FileName FileName => _fileName;

      /// <summary>
      /// The file contents. May be null if this file is no part of the search
      /// engine text index.
      /// </summary>
      public FileContents Contents => _contents;

      public bool IsSearchable => _project.IsFileSearchable(_fileName);

      public ProjectFileData WithContents(FileContents contents) {
        return new ProjectFileData(_project, _fileName, contents);
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