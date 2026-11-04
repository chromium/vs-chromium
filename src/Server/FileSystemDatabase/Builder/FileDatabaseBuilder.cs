// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Server.Cache;
using VsChromium.Server.Diagnostics;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.TextSearch;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystemDatabase.Builder {
  public class FileDatabaseBuilder {
    /// <summary>
    /// Split large files in chunks of maximum <code>ChunkSize</code> bytes.
    /// </summary>
    private const int ChunkSize = FileContents.MaxChunkLength;

    private readonly IFileSystem _fileSystem;
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private readonly IIndexingWorkerPool _indexingWorkerPool;
    private readonly IIndexCache _indexCache;

    public FileDatabaseBuilder(
      IFileSystem fileSystem,
      IFileContentsFactory fileContentsFactory,
      IProgressTrackerFactory progressTrackerFactory,
      IIndexingWorkerPool indexingWorkerPool,
      IIndexCache indexCache = null) {
      _fileSystem = fileSystem;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
      _indexingWorkerPool = indexingWorkerPool;
      _indexCache = indexCache;
    }

    public IFileDatabaseSnapshot Build(IFileDatabaseSnapshot previousDatabase,
                                       FileSystemSnapshot newSnapshot,
                                       FullPathChanges fullPathChanges,
                                       Action onLoading,
                                       Action onLoaded,
                                       Action<IFileDatabaseSnapshot> onIntermadiateResult,
                                       CancellationToken cancellationToken) {
      using (new TimeElapsedLogger("Building file database from previous one and file system tree snapshot",
        cancellationToken, InfoLogger.Instance)) {
        using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
          onLoading();
          progress.DisplayProgress((i, n) => "Preparing list of files to load from disk");

          var fileDatabase = (FileDatabaseSnapshot) previousDatabase;

          // Compute list of files from tree
          FileSystemEntities entities;
          using (OperationTimings.Measure(OperationTimings.FileDatabaseUpdate + ".Entities")) {
            entities = ComputeFileSystemEntities(newSnapshot, cancellationToken);
          }

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
            CacheLookups = GetCacheLookups(newSnapshot),
            NewSnapshot = newSnapshot,
            PartialProgressReporter = new PartialProgressReporter(
              TimeSpan.FromSeconds(5.0),
              () => {
                Logger.LogInfo("Creating intermedidate file database for partial progress reporting");
                var database = CreateFileDatabase(entities, false, cancellationToken);
                onIntermadiateResult(database);
              })
          };

          // Merge old state in new state and load all missing files
          using (OperationTimings.Measure(OperationTimings.FileDatabaseUpdate + ".Load")) {
            LoadFileContents(entities, loadingContext, cancellationToken);
          }

          FileDatabaseSnapshot result;
          using (OperationTimings.Measure(OperationTimings.FileDatabaseUpdate + ".Freeze")) {
            result = CreateFileDatabase(entities, true, cancellationToken);
          }
          // Note: Before "onLoaded", as clients may search as soon as they are notified.
          using (OperationTimings.Measure(OperationTimings.FileDatabaseUpdate + ".Precompute")) {
            result.PrecomputeSearchData();
          }
          onLoaded();
          return result;
        }
      }
    }

    public IFileDatabaseSnapshot BuildWithChangedFiles(IFileDatabaseSnapshot previousFileDatabaseSnapshot,
                                                       FileSystemSnapshot fileSystemSnapshot,
                                                       IEnumerable<ProjectFileName> changedFiles,
                                                       Action onLoading,
                                                       Action onLoaded,
                                                       Action<IFileDatabaseSnapshot> onIntermadiateResult,
                                                       CancellationToken cancellationToken) {
      using (new TimeElapsedLogger("Building file database from previous one and list of changed files",
        cancellationToken, InfoLogger.Instance)) {
        Invariants.Assert(previousFileDatabaseSnapshot is FileDatabaseSnapshot);
        var previousFileDatabase = (FileDatabaseSnapshot) previousFileDatabaseSnapshot;

        // Update file contents of file data entries of changed files.
        var filesToRead = new List<(ProjectFileName File, int Index)>();
        foreach (var file in changedFiles) {
          if (file.Project.IsFileSearchable(file.FileName) && previousFileDatabase.TryGetFileIndex(file.FileName, out var index)) {
            filesToRead.Add((file, index));
          }
        }

        if (filesToRead.Count == 0) {
          Logger.LogInfo("None of the changed file is searchable, return previous database snapshot");
          return previousFileDatabaseSnapshot;
        }

        // Read file contents.
        onLoading();
        var changes = new (FileContents OldContents, FileContents NewContents)[filesToRead.Count];
        void ReadFile(int i) {
          var (file, index) = filesToRead[i];
          var newContents = _fileContentsFactory.ReadFileContents(file.FileName.FullPath);
          changes[i] = (previousFileDatabase.GetContents(index), newContents);
          previousFileDatabase.DangerousSetContents(index, newContents);
        }
        // Note: Many files can change at once (e.g. switching branches): read them in parallel
        // on the indexing worker pool; keep small edits inline for low latency.
        if (filesToRead.Count > 16) {
          _indexingWorkerPool.RunBatches(filesToRead.Count, batchSize: 16, cancellationToken, (start, end) => {
            for (var i = start; i < end; i++) {
              ReadFile(i);
            }
            return true;
          });
        } else {
          for (var i = 0; i < filesToRead.Count; i++) {
            ReadFile(i);
          }
        }

        // Return new file database with updated file contents.
        var result = new FileDatabaseSnapshot(
          previousFileDatabase.ProjectHashes,
          previousFileDatabase.Directories,
          // Same files, only contents changed (the arrays are shared)
          previousFileDatabase.FileNameArray,
          previousFileDatabase.ContentsArray,
          // Same pieces, if possible
          CanReuseFilePieces(changes) ? previousFileDatabase.FilePieces : null);
        // Note: Before "onLoaded", as clients may search as soon as they are notified.
        result.PrecomputeSearchData();
        onLoaded();
        return result;
      }
    }

    /// <summary>
    /// Returns true if the pieces of the previous snapshot can be used with the new contents of
    /// the changed files: the pieces of small files (a single piece) refer to them by index (the
    /// new contents are found in the contents array), so this is possible if each changed file
    /// is "small" (a single piece) before and after the change, and has contents before and after
    /// the change. This avoids recomputing the list of pieces of all the files when a few files
    /// are modified (e.g. saved from an editor), which is the most common change.
    /// </summary>
    private static bool CanReuseFilePieces((FileContents OldContents, FileContents NewContents)[] changes) {
      foreach (var (oldContents, newContents) in changes) {
        if (!IsSinglePieceFile(oldContents) || !IsSinglePieceFile(newContents))
          return false;
      }
      return true;
    }

    private static bool IsSinglePieceFile(FileContents contents) {
      return HasContents(contents) && contents.ByteLength <= ChunkSize;
    }

    private FileDatabaseSnapshot CreateFileDatabase(FileSystemEntities entities,
                                                    bool notifyProgress,
                                                    CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      using (new TimeElapsedLogger("Freezing file database state", cancellationToken)) {
        var progress = notifyProgress ? _progressTrackerFactory.CreateIndeterminateTracker() : null;
        try {
          progress?.DisplayProgress((i, n) => "Finalizing index update");
          // Note: The arrays of the entities are the arrays of the snapshot (no copy). The
          // intermediate snapshots (partial progress reporting) share them with the final one:
          // they see the contents loaded so far.
          return new FileDatabaseSnapshot(entities.ProjectHashes, entities.Directories, entities.FileNames, entities.Contents);
        }
        finally {
          progress?.Dispose();
        }
      }
    }

    /// <summary>
    /// Creates the pieces of the files with contents. Partitioning evenly ensures that each
    /// processor will deal with a partition of equal "weight". In this case, we make sure each
    /// partition contains not only the same amount of files, but also (as close to as possible)
    /// the same amount of "bytes". For example, if we have 100 files totaling 32MB and 4
    /// processors, we will end up with 4 partitions of (exactly) 25 files totalling
    /// (approximately) 8MB each.
    /// </summary>
    public static FilePieceList CreateFilePieces(FileName[] fileNames, FileContents[] contents) {
      // Count the total # of small files and the chunks of large files.
      var smallFilesCount = 0;
      var chunks = new List<FilePieceList.Chunk>();
      for (var i = 0; i < contents.Length; i++) {
        var fileContents = contents[i];
        if (!HasContents(fileContents))
          continue;
        if (fileContents.ByteLength <= ChunkSize) {
          smallFilesCount++;
        } else {
          // Note: Same chunks as the trigram filters of the file contents (chunk index).
          var ranges = fileContents.GetChunks();
          for (var chunkIndex = 0; chunkIndex < ranges.Count; chunkIndex++) {
            chunks.Add(new FilePieceList.Chunk(i, chunkIndex, ranges[chunkIndex], fileContents));
          }
        }
      }

      var totalCount = smallFilesCount + chunks.Count;

      // Store elements in their partitions
      // # of partitions = # of logical processors
      var items = new int[totalCount];
      var partitionCount = Environment.ProcessorCount;
      var generator = new PartitionIndicesGenerator(totalCount, partitionCount);

      // Store large files
      for (var i = 0; i < chunks.Count; i++) {
        items[generator.Next()] = ~i;
      }

      // Store small files
      for (var i = 0; i < contents.Length; i++) {
        var fileContents = contents[i];
        if (HasContents(fileContents) && fileContents.ByteLength <= ChunkSize) {
          items[generator.Next()] = i;
        }
      }

      var result = new FilePieceList(fileNames, contents, items, chunks.ToArray());
      FileDatabaseDebugLogger.LogFilePieces(result, partitionCount);
      return result;
    }

    public static bool HasContents(FileContents contents) {
      return contents != null && contents.ByteLength > 0;
    }

    /// <summary>
    /// The files of a snapshot, and their contents being loaded.
    /// </summary>
    private class FileSystemEntities {
      /// <summary>
      /// All the files, grouped by directory, sorted by name in each directory (see
      /// <see cref="Directories"/>).
      /// </summary>
      public FileName[] FileNames { get; set; }
      /// <summary>
      /// The project of each file (parallel to <see cref="FileNames"/>), during the load only.
      /// </summary>
      public IProject[] Projects { get; set; }
      /// <summary>
      /// The last write time and size of each file from the scan (parallel to <see
      /// cref="FileNames"/>), during the load only: to validate the contents of the index cache.
      /// </summary>
      public FileMetadata[] Metadata { get; set; }
      /// <summary>
      /// Parallel array to <see cref="FileNames"/>: each worker writes <c>Contents[i]</c> (an
      /// atomic reference assignment) without lock contention, and <see cref="CreateFileDatabase"/>
      /// can safely snapshot partial progress concurrently.
      /// </summary>
      public FileContents[] Contents { get; set; }
      public Dictionary<DirectoryName, FileRange> Directories { get; set; }
      public IDictionary<FullPath, string> ProjectHashes { get; set; }
    }

    private FileSystemEntities ComputeFileSystemEntities(FileSystemSnapshot snapshot,
                                                         CancellationToken cancellationToken) {
      using (new TimeElapsedLogger("Computing tables of directory names and file names from FileSystemTree",
        cancellationToken)) {

        var directories = FileSystemSnapshotVisitor.GetDirectories(snapshot);

        var fileCount = 0;
        var directoryCount = 0;
        foreach (var directory in directories.ToForeachEnum()) {
          if (directory.Value.ChildFiles.Count > 0) {
            fileCount += directory.Value.ChildFiles.Count;
            directoryCount++;
          }
        }

        var fileNames = new FileName[fileCount];
        var projects = new IProject[fileCount];
        var metadata = new FileMetadata[fileCount];
        var ranges = new Dictionary<DirectoryName, FileRange>(directoryCount);
        var index = 0;
        foreach (var directory in directories.ToForeachEnum()) {
          var childFiles = directory.Value.ChildFiles;
          if (childFiles.Count == 0)
            continue;
          var childFileMetadata = directory.Value.ChildFileMetadata;
          var start = index;
          var sorted = true;
          for (var i = 0; i < childFiles.Count; i++) {
            var fileName = childFiles[i];
            if (i > 0 && SystemPathComparer.CompareNames(fileNames[index - 1].Name, fileName.Name) > 0)
              sorted = false;
            fileNames[index] = fileName;
            projects[index] = directory.Key;
            metadata[index] = childFileMetadata[i];
            index++;
          }
          // The files of a directory must be sorted by name (binary search in
          // FileDatabaseSnapshot.TryGetFileIndex). They usually are already (see
          // ProjectRootSnapshotBuilder).
          if (!sorted) {
            // Note: The metadata are sorted with their file.
            Array.Sort(fileNames, metadata, start, childFiles.Count, Comparer<FileName>.Create((x, y) => SystemPathComparer.CompareNames(x.Name, y.Name)));
          }
          // Note: The same directory may appear in two projects (nested project roots, with
          // different absolute names): the last one wins (as the former table of files).
          ranges[directory.Value.DirectoryName] = new FileRange(start, childFiles.Count);
        }
        Invariants.Assert(index == fileCount);

        return new FileSystemEntities {
          FileNames = fileNames,
          Projects = projects,
          Metadata = metadata,
          Contents = new FileContents[fileCount],
          Directories = ranges,
          ProjectHashes = snapshot.ProjectRoots.ToDictionary(x => x.Project.RootPath, x => x.Project.VersionHash)
        };
      }
    }

    private class FileContentsLoadingContext {
      public FileDatabaseSnapshot OldFileDatabaseSnapshot;
      public FullPathChanges FullPathChanges;
      public ISet<IProject> UnchangedProjects;
      public int LoadedTextFileCount;
      public int LoadedBinaryFileCount;
      public int LoadedFromCacheFileCount;
      public int NotLoadedFileCount;
      public FileSystemSnapshot NewSnapshot;
      /// <summary>
      /// The entries of the index cache of the projects that have one (null if none).
      /// </summary>
      public Dictionary<IProject, IndexCacheLookup> CacheLookups;
      public PartialProgressReporter PartialProgressReporter;
    }

    /// <summary>
    /// The entries of the index cache of the projects of <paramref name="snapshot"/> (the cache
    /// of each project is opened on first use), or null if there is none.
    /// </summary>
    private Dictionary<IProject, IndexCacheLookup> GetCacheLookups(FileSystemSnapshot snapshot) {
      if (_indexCache == null || !_indexCache.IsEnabled)
        return null;
      var result = new Dictionary<IProject, IndexCacheLookup>(new ReferenceEqualityComparer<IProject>());
      foreach (var projectRoot in snapshot.ProjectRoots) {
        var lookup = _indexCache.GetProjectCache(projectRoot.Project.RootPath)?.Lookup;
        if (lookup != null) {
          result[projectRoot.Project] = lookup;
        }
      }
      return result.Count == 0 ? null : result;
    }

    private void LoadFileContents(FileSystemEntities entities,
                                  FileContentsLoadingContext loadingContext,
                                  CancellationToken cancellationToken) {
      using (new TimeElapsedLogger("Loading file contents from disk", cancellationToken)) {
        using (var progress = _progressTrackerFactory.CreateTracker(entities.FileNames.Length)) {
          try {
            _indexingWorkerPool.RunBatches(entities.FileNames.Length, batchSize: 32, cancellationToken, (start, end) => {
              for (var i = start; i < end; i++) {
                var fileName = entities.FileNames[i];

                // ReSharper disable once AccessToDisposedClosure
                if (progress.Step()) {
                  // ReSharper disable once AccessToDisposedClosure
                  progress.DisplayProgress((step, n) =>
                    string.Format("Reading file {0:n0} of {1:n0}: {2}", step, n, fileName.FullPath));

                  // Check for cancellation
                  if (cancellationToken.IsCancellationRequested) {
                    cancellationToken.ThrowIfCancellationRequested();
                  }
                }

                var contents = LoadSingleFileContents(loadingContext, entities.Projects[i], fileName, entities.Metadata[i]);
                if (contents != null) {
                  entities.Contents[i] = contents;
                }
              }
              return true;
            });
          }
          catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            loadingContext.PartialProgressReporter.ReportProgressNow();
            throw;
          }
        }
      }

      // The projects and metadata are only needed during the load.
      entities.Projects = null;
      entities.Metadata = null;

      // The entries of the index cache are not needed anymore: the contents of the next loads are
      // in memory (the cache writes don't need them). Released now for their memory.
      if (loadingContext.CacheLookups != null) {
        foreach (var project in loadingContext.CacheLookups.Keys) {
          _indexCache.GetProjectCache(project.RootPath)?.ReleaseLookup();
        }
        loadingContext.CacheLookups = null;
      }

      Logger.LogInfo("Loaded {0:n0} text files from disk, skipped {1:n0} binary files, {2:n0} files from the index cache" +
                     " ({3:n0} files not loaded: tree of the index cache, verified later).",
        loadingContext.LoadedTextFileCount,
        loadingContext.LoadedBinaryFileCount,
        loadingContext.LoadedFromCacheFileCount,
        loadingContext.NotLoadedFileCount);
      if (_indexCache != null && _indexCache.IsEnabled) {
        _indexCache.RecordLoad(loadingContext.LoadedFromCacheFileCount,
          loadingContext.LoadedTextFileCount + loadingContext.LoadedBinaryFileCount);
      }
    }

    private FileContents LoadSingleFileContents(FileContentsLoadingContext loadingContext,
                                                IProject project,
                                                FileName fileName,
                                                FileMetadata metadata) {
      var oldDatabase = loadingContext.OldFileDatabaseSnapshot;
      var oldContents = oldDatabase.TryGetFileIndex(fileName, out var oldIndex) ? oldDatabase.GetContents(oldIndex) : null;

      // If the file was never loaded before, just load it (after performing a
      // "isSearchable" check).
      if (oldContents == null) {
        return LoadSingleFileContentsWorker(loadingContext, project, fileName, metadata);
      }

      bool isSearchable;
      // If the project configuration is unchanged from the previous file
      // database (and the file was present in it), then it is certainly
      // searchable, so no need to make an expensive call to "IsSearchable"
      // again.
      if (loadingContext.UnchangedProjects.Contains(project)) {
        isSearchable = true;
      } else {
        // If the file is not searchable in the current project, it should be
        // ignored too. Note that "IsSearachable" is a somewhat expensive
        // operation, as the filename is checked against potentially many glob
        // patterns.
        isSearchable = project.IsFileSearchable(fileName);
      }

      if (!isSearchable) {
        return null;
      }

      // Contents loaded from a cache deleted since ("Rebuild index") are read again.
      if (_indexCache != null && _indexCache.IsFromInvalidatedCache(oldContents)) {
        return LoadSingleFileContentsWorker(loadingContext, project, fileName, metadata);
      }

      // If the file has not changed since the previous snapshot, we can re-use
      // the former file contents snapshot.
      if (IsFileContentsUpToDate(loadingContext.FullPathChanges, fileName, oldContents)) {
        return oldContents;
      }

      return LoadSingleFileContentsWorker(loadingContext, project, fileName, metadata);
    }

    private FileContents LoadSingleFileContentsWorker(FileContentsLoadingContext loadingContext,
                                                      IProject project,
                                                      FileName fileName,
                                                      FileMetadata metadata) {
      // If project configuration has not changed, the file is still not
      // searchable, irrelevant to calling "IsSearchable".
      if (loadingContext.FullPathChanges != null) {
        if (loadingContext.FullPathChanges.ShouldSkipLoadFileContents(fileName)) {
          if (loadingContext.UnchangedProjects.Contains(project))
            return null;
        }
      }

      // This is an expensive call, hopefully avoided by the code above.
      if (!project.IsFileSearchable(fileName)) {
        return null;
      }

      loadingContext.PartialProgressReporter.ReportProgress();

      // The index cache has the contents of the file if it has an entry with the same relative
      // path, last write time and size (no file opened).
      if (loadingContext.CacheLookups != null && loadingContext.CacheLookups.TryGetValue(project, out var cacheLookup)) {
        var cachedContents = cacheLookup.TryGetContents(fileName, metadata);
        if (cachedContents != null) {
          Interlocked.Increment(ref loadingContext.LoadedFromCacheFileCount);
          return cachedContents;
        }
      }

      // Projects whose tree comes from the index cache (phase 2): no disk access, the files
      // without cached contents (e.g. larger than 512 KB) are read by the verification of the tree
      // (they are in its changes). The files of the changes (e.g. from the directory watcher) are
      // read.
      if (loadingContext.NewSnapshot.IsCachedProject(project.RootPath) &&
          (loadingContext.FullPathChanges == null || loadingContext.FullPathChanges.ShouldSkipLoadFileContents(fileName))) {
        Interlocked.Increment(ref loadingContext.NotLoadedFileCount);
        return null;
      }

      var fileContents = _fileContentsFactory.ReadFileContents(fileName.FullPath);
      if (fileContents is BinaryFileContents) {
        Interlocked.Increment(ref loadingContext.LoadedBinaryFileCount);
      } else {
        Interlocked.Increment(ref loadingContext.LoadedTextFileCount);
      }

      return fileContents;
    }

    private bool IsFileContentsUpToDate(FullPathChanges fullPathChanges,
                                        FileName fileName,
                                        FileContents existingContents) {
      Invariants.Assert(existingContents != null);

      if (fullPathChanges != null) {
        // We don't get file change events for file in symlinks, so we can't
        // rely on fullPathChanges contents for our heuristic of avoiding file
        // system access.
        // Actually, since we cannot reliable detect changes in symlinks, we enable this
        // optimization anyways, as we rely on the user to manually refresh the index
        // (that results in a "null" value for fullPathChanges)
        return fullPathChanges.ShouldSkipLoadFileContents(fileName);
      }

      // Do the "expensive" check by going to the file system.
      var fi = _fileSystem.GetFileInfoSnapshot(fileName.FullPath);
      return
        (fi.Exists) &&
        (fi.IsFile) &&
        (fi.LastWriteTimeUtc == existingContents.UtcLastModified);
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
