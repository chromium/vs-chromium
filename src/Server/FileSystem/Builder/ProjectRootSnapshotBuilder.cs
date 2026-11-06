// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem.Builder {
  public class ProjectRootSnapshotBuilder {
    private readonly IFileSystem _fileSystem;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly FileSystemSnapshot _oldSnapshot;
    private readonly IProject _project;
    private readonly IProgressTracker _progress;
    private readonly ProjectPathChanges _pathChanges;
    private readonly IIndexingWorkerPool _indexingWorkerPool;
    private readonly CancellationToken _cancellationToken;

    public ProjectRootSnapshotBuilder(IFileSystem fileSystem,
                                     IFileSystemNameFactory fileSystemNameFactory,
                                     FileSystemSnapshot oldSnapshot,
                                     IProject project,
                                     IProgressTracker progress,
                                     ProjectPathChanges pathChanges,
                                     IIndexingWorkerPool indexingWorkerPool,
                                     CancellationToken cancellationToken) {
      _fileSystem = fileSystem;
      _fileSystemNameFactory = fileSystemNameFactory;
      _oldSnapshot = oldSnapshot;
      _project = project;
      _progress = progress;
      _pathChanges = pathChanges;
      _indexingWorkerPool = indexingWorkerPool;
      _cancellationToken = cancellationToken;
      _rootVolume = new Lazy<ulong?>(() =>
        _fileSystem.TryGetDirectoryTarget(_project.RootPath, out var volume, out _) ? volume : null);
    }

    /// <summary>The volume serial number of the project root, if known.</summary>
    private readonly Lazy<ulong?> _rootVolume;

    /// <summary>
    /// The id of a directory of the tree (see <see cref="DirectoryData.FileId"/>): its id in the
    /// listing of its parent, or for a directory symbolic link or junction, the id of its target if
    /// it is on the volume of the project (0 otherwise).
    /// </summary>
    private UInt128 GetDirectoryId(DirectoryName directory, bool isSymLink, UInt128 entryId) {
      if (!isSymLink)
        return entryId;
      return GetSymLinkTargetId(_fileSystem, _project.RootPath.Combine(directory.RelativePath), _rootVolume.Value);
    }

    /// <summary>
    /// The id of the target of a directory symbolic link or junction if it is on the volume
    /// <paramref name="projectVolume"/>, otherwise 0.
    /// </summary>
    public static UInt128 GetSymLinkTargetId(IFileSystem fileSystem, FullPath path, ulong? projectVolume) {
      if (projectVolume == null || !fileSystem.TryGetDirectoryTarget(path, out var volume, out var id) || volume != projectVolume.Value)
        return UInt128.Zero;
      return id;
    }

    public DirectorySnapshot Build() {
      if (_pathChanges != null) {
        // If we have a project with the same root in the old snapshot, use that
        // snapshot instead of traversing the file system.
        var oldRoot = _oldSnapshot.ProjectRoots
          .FirstOrDefault(x => x.Project.RootPath.Equals(_project.RootPath));
        if (oldRoot != null) {
          return ApplyDirectorySnapshotDelta(oldRoot.Directory);
        }
      }

      return CreateProjectDirectorySnapshot();
    }

    private DirectorySnapshot CreateProjectDirectorySnapshot() {
      using (new TimeElapsedLogger($"Creating directory snapshot from file system files of \"{_project.RootPath}\"", _cancellationToken)) {
        var projectPath = _fileSystemNameFactory.CreateAbsoluteDirectoryName(_project.RootPath);
        // Note: The id of the project root (the other directories have the id of their entry in
        // their parent).
        return CreateDirectorySnapshot(projectPath, false, _fileSystem.GetFileInfoSnapshot(_project.RootPath).FileId);
      }
    }

    private DirectorySnapshot CreateDirectorySnapshot(DirectoryName directory, bool isSymLink, UInt128 fileId) {
      _cancellationToken.ThrowIfCancellationRequested();

      using (new TimeElapsedLogger($"Traversing directory \"{directory.FullPath}\" to collect directory/file names", _cancellationToken, InfoLogger.Instance)) {
        // The snapshot tree is built bottom up during the traversal: each
        // directory task waits for its child directory tasks, then creates its
        // own snapshot (same design as mtsuite "IDirectorCollector<T>").
        var task = TraverseDirectoryAsync(new DirectoryData(directory, isSymLink, fileId), _cancellationToken);
        task.Wait(_cancellationToken);
        var result = task.Result;
        Invariants.Assert(result != null);
        Invariants.Assert(result.DirectoryName.Equals(directory));
        return result;
      }
    }

    private DirectorySnapshot ApplyDirectorySnapshotDelta(DirectorySnapshot oldDirectory) {
      var oldDirectoryPath = oldDirectory.DirectoryName.RelativePath;

      // Directories with no created or deleted entry in their subtree are unchanged (snapshots
      // are immutable): reuse them, instead of visiting (and building the relative path of) all
      // the directories of the project for each change.
      if (!_pathChanges.IsSubtreeAffected(oldDirectoryPath)) {
        return oldDirectory;
      }

      // Create lists of created dirs and files. We have to access the file system to know
      // if each path is a file or a directory.
      List<IFileInfoSnapshot> createDirs = null;
      List<IFileInfoSnapshot> createdFiles = null;
      foreach (var path in _pathChanges.GetCreatedEntries(oldDirectoryPath).ToForeachEnum()) {
        _cancellationToken.ThrowIfCancellationRequested(); // cancellation

        var info = _fileSystem.GetFileInfoSnapshot(_project.RootPath.Combine(path));
        if (info.IsDirectory) {
          if (createDirs == null)
            createDirs = new List<IFileInfoSnapshot>();
          createDirs.Add(info);
        } else if (info.IsFile) {
          if (createdFiles == null)
            createdFiles = new List<IFileInfoSnapshot>();
          createdFiles.Add(info);
        }
      }

      // Recursively create new directory entires for previous (non deleted)
      // entries.
      var childDirectories = oldDirectory.ChildDirectories
        .Where(dir => !_pathChanges.IsDeleted(dir.DirectoryName.RelativePath))
        .Select(dir => ApplyDirectorySnapshotDelta(dir))
        .ToList();

      // Add created directories
      if (createDirs != null) {
        foreach (var info in createDirs.ToForeachEnum()) {
          _cancellationToken.ThrowIfCancellationRequested(); // cancellation

          var createdDirectoryName = _fileSystemNameFactory.CreateDirectoryName(oldDirectory.DirectoryName, info.Path.FileName, info.IsSymLink);
          var childSnapshot = CreateDirectorySnapshot(createdDirectoryName, info.IsSymLink,
            GetDirectoryId(createdDirectoryName, info.IsSymLink, info.IsSymLink ? UInt128.Zero : info.FileId));

          // Note: File system change notifications are not always 100%
          // reliable. We may get a "create" event for directory we already know
          // about.
          var index = childDirectories.FindIndex(x => SystemPathComparer.EqualsNames(x.DirectoryName.Name, createdDirectoryName.Name));
          if (index >= 0) {
            childDirectories.RemoveAt(index);
          }
          childDirectories.Add(childSnapshot);
        }

        // We need to re-sort the array since we added new entries
        childDirectories.Sort((x, y) => SystemPathComparer.Compare(x.DirectoryName.Name, y.DirectoryName.Name));
      }

      // Match non deleted files
      // Sepcial case: if no file deleted or created, just re-use the list.
      IList<FileName> newFileList;
      FileMetadata[] newFileMetadata;
      int[] newSymLinkFiles;
      if (_pathChanges.GetDeletedEntries(oldDirectoryPath).Count == 0 && createdFiles == null) {
        newFileList = oldDirectory.ChildFiles;
        newFileMetadata = oldDirectory.ChildFileMetadataArray;
        newSymLinkFiles = oldDirectory.SymLinkFileIndicesArray;
      } else {
        // Copy the list of previous children, minus deleted files.
        var newFileListTemp = new List<(FileName Name, FileMetadata Metadata, bool IsSymLink)>();
        for (var i = 0; i < oldDirectory.ChildFiles.Count; i++) {
          var name = oldDirectory.ChildFiles[i];
          if (!_pathChanges.IsDeleted(name.RelativePath)) {
            newFileListTemp.Add((name, oldDirectory.ChildFileMetadata[i], oldDirectory.IsSymLinkFile(i)));
          }
        }

        // Add created files
        if (createdFiles != null) {
          foreach (var info in createdFiles.ToForeachEnum()) {
            var name = _fileSystemNameFactory.CreateFileName(oldDirectory.DirectoryName, info.Path.FileName);
            // Note: File system change notifications are not always 100%
            // reliable. We may get a "create" event for files we already know
            // about: the new metadata replaces the previous one.
            newFileListTemp.RemoveAll(x => SystemPathComparer.EqualsNames(x.Name.Name, name.Name));
            newFileListTemp.Add((name, new FileMetadata(info.LastWriteTimeUtc, info.Length), info.IsSymLink));
          }

          // We need to re-sort the array since we added new entries
          newFileListTemp.Sort((x, y) => SystemPathComparer.Compare(x.Name.Name, y.Name.Name));
        }
        newFileList = newFileListTemp.Select(x => x.Name).ToReadOnlyList();
        newFileMetadata = newFileListTemp.Select(x => x.Metadata).ToArray();
        newSymLinkFiles = GetSymLinkIndices(newFileListTemp, x => x.IsSymLink);
      }

      var newData = oldDirectory.DirectoryData;

      return new DirectorySnapshot(
        newData,
        childDirectories.ToReadOnlyList(),
        newFileList.ToReadOnlyList(),
        newFileMetadata,
        newSymLinkFiles);
    }

    /// <summary>The indices of the items that are symbolic links, null if none.</summary>
    internal static int[] GetSymLinkIndices<T>(IList<T> items, Func<T, bool> isSymLink) {
      List<int> result = null;
      for (var i = 0; i < items.Count; i++) {
        if (isSymLink(items[i])) {
          (result ??= new List<int>()).Add(i);
        }
      }
      return result?.ToArray();
    }

    /// <summary>
    /// Returns a task that enumerates the files and directories of
    /// <paramref name="directory"/> (recursively) and returns the corresponding
    /// snapshot, or <code>null</code> if the directory is excluded by the
    /// project directory filter.
    /// </summary>
    private Task<DirectorySnapshot> TraverseDirectoryAsync(DirectoryData directory, CancellationToken token) {
      return Task.Factory.StartNew(
        () => ProcessDirectoryAsync(directory, token),
        token,
        TaskCreationOptions.DenyChildAttach,
        _indexingWorkerPool.Scheduler).Unwrap();
    }

    private async Task<DirectorySnapshot> ProcessDirectoryAsync(DirectoryData directory, CancellationToken token) {
      if (!directory.DirectoryName.IsAbsoluteName &&
          !directory.DirectoryName.TestRelativePath(static (path, filter) => filter.Include(path), _project.DirectoryFilter)) {
        return null;
      }

      List<Task<DirectorySnapshot>> childTasks = null;
      var childEntries = GetDirectoryEntries(directory);
      var childFiles = new List<(FileName Name, FileMetadata Metadata, bool IsSymLink)>();
      // Note: Use "for" loop to avoid memory allocations.
      for (var i = 0; i < childEntries.Count; i++) {
        var entry = childEntries[i];
        if (entry.IsDirectory) {
          var subDirName = _fileSystemNameFactory.CreateDirectoryName(directory.DirectoryName, entry.Name, entry.IsSymLink);
          var subDir = new DirectoryData(subDirName, entry.IsSymLink, GetDirectoryId(subDirName, entry.IsSymLink, entry.FileId));
          if (childTasks == null)
            childTasks = new List<Task<DirectorySnapshot>>();
          childTasks.Add(TraverseDirectoryAsync(subDir, token));
        } else if (entry.IsFile) {
          childFiles.Add((_fileSystemNameFactory.CreateFileName(directory.DirectoryName, entry.Name), entry.Metadata, entry.IsSymLink));
        }
      }

      var (fileNames, fileMetadata, symLinkFiles) = FilterAndSortFiles(childFiles);

      // Note: The progress of the traversal counts the files found (displayed by the clients).
      if (_progress.Step(fileNames.Count)) {
        _progress.DisplayProgress((i, n) => string.Format("Traversing directory: {0}\\{1}",
          _project.RootPath.Value,
          directory.DirectoryName.RelativePath.Value));
      }

      IList<DirectorySnapshot> childDirectories;
      if (childTasks == null) {
        childDirectories = ArrayUtilities.EmptyList<DirectorySnapshot>.Instance;
      } else {
        // Schedule the continuation on TaskScheduler.Current (the indexing worker pool) rather
        // than escaping back to the default thread pool.
        var childSnapshots = await Task.WhenAll(childTasks);
        childDirectories = childSnapshots
          .Where(x => x != null)
          .OrderBy(x => x.DirectoryName.Name, SystemPathComparer.NameComparer)
          .ToReadOnlyList();
      }

      return new DirectorySnapshot(directory, childDirectories, fileNames, fileMetadata, symLinkFiles);
    }

    /// <summary>
    /// The files of a directory included by the file filter of the project, sorted by name, with
    /// their metadata and the indices of the symbolic links.
    /// </summary>
    private (IList<FileName> Names, FileMetadata[] Metadata, int[] SymLinks) FilterAndSortFiles(
      List<(FileName Name, FileMetadata Metadata, bool IsSymLink)> childFiles) {
      var files = childFiles
        .Where(x => x.Name.TestRelativePath(static (path, filter) => filter.Include(path), _project.FileFilter))
        // Note: Ordinal (case-insensitive on Windows/macOS) sort, same as ApplyDirectorySnapshotDelta
        // (see "Behavior changes" in PORTING_PRINCIPLES.md).
        .OrderBy(x => x.Name.Name, SystemPathComparer.NameComparer)
        .ToList();
      var fileNames = files.Select(x => x.Name).ToReadOnlyList();
      var fileMetadata = files.Count == 0 ? Array.Empty<FileMetadata>() : files.Select(x => x.Metadata).ToArray();
      return (fileNames, fileMetadata, GetSymLinkIndices(files, x => x.IsSymLink));
    }

    private IList<DirectoryEntry> GetDirectoryEntries(DirectoryData directory) {
      var path = _project.RootPath.Combine(directory.DirectoryName.RelativePath);
      try {
        return _fileSystem.GetDirectoryEntries(path);
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Skipping directory \"{0}\"", path);
        return ArrayUtilities.EmptyList<DirectoryEntry>.Instance;
      }
    }

    /// <summary>
    /// The catch-up of the verification of the tree of the index cache from the change journal:
    /// the tree of the project built from <paramref name="oldRoot"/> (the tree of the cache) by
    /// listing again only <paramref name="changedDirectories"/> (the directories with a record in
    /// the journal since the checkpoint of the cache, see <see cref="ChangeJournalDirectoryMapper"/>)
    /// and the directories without file id; the new subdirectories (or replaced: other file id) of
    /// the directories listed again, and the subtrees of the directory symbolic links and junctions
    /// whose target is on another volume or changed (<paramref name="symLinksToScan"/>, see <see
    /// cref="ChangeJournalDirectoryMapper.CheckSymLinks"/>) are scanned entirely; the file symbolic
    /// links are checked again (the metadata of their target). The other directories (including
    /// the subtrees of the directory symbolic links to the volume of the project: their records
    /// have the ids of their target) are reused as is. The result is the same as a full scan (<see
    /// cref="Build"/>), provided the journal recorded all the changes since the checkpoint.
    /// </summary>
    public DirectorySnapshot CatchUp(DirectorySnapshot oldRoot, IReadOnlySet<DirectorySnapshot> changedDirectories,
                                     IReadOnlySet<DirectorySnapshot> symLinksToScan, CatchUpStatistics statistics) {
      using (new TimeElapsedLogger($"Catching up the tree of \"{_project.RootPath}\" from the change journal", _cancellationToken)) {
        var visit = new HashSet<DirectorySnapshot>();
        MarkDirectoriesToVisit(oldRoot, changedDirectories, symLinksToScan, visit);
        var context = new CatchUpContext(changedDirectories, symLinksToScan, visit, statistics);
        var task = Task.Factory.StartNew(
          () => CatchUpDirectoryAsync(oldRoot, context),
          _cancellationToken,
          TaskCreationOptions.DenyChildAttach,
          _indexingWorkerPool.Scheduler).Unwrap();
        task.Wait(_cancellationToken);
        Invariants.Assert(task.Result != null);
        return task.Result;
      }
    }

    private sealed class CatchUpContext {
      public CatchUpContext(IReadOnlySet<DirectorySnapshot> changed, IReadOnlySet<DirectorySnapshot> symLinksToScan,
                            HashSet<DirectorySnapshot> visit, CatchUpStatistics statistics) {
        Changed = changed;
        SymLinksToScan = symLinksToScan;
        Visit = visit;
        Statistics = statistics;
      }

      public IReadOnlySet<DirectorySnapshot> Changed { get; }
      public IReadOnlySet<DirectorySnapshot> SymLinksToScan { get; }

      /// <summary>A directory symbolic link or junction to scan entirely (other volume, or changed target).</summary>
      public bool MustScan(DirectorySnapshot directory) => MustScanSymLink(directory, SymLinksToScan);
      /// <summary>The directories to visit (read only during the catch-up: thread safe).</summary>
      public HashSet<DirectorySnapshot> Visit { get; }
      public CatchUpStatistics Statistics { get; }
    }

    /// <summary>
    /// Adds to <paramref name="visit"/> the directories of the tree that need work (listed again,
    /// scanned, or with file symbolic links to check) and their ancestors.
    /// </summary>
    private static bool MarkDirectoriesToVisit(DirectorySnapshot directory, IReadOnlySet<DirectorySnapshot> changed,
                                               IReadOnlySet<DirectorySnapshot> symLinksToScan, HashSet<DirectorySnapshot> visit) {
      var scan = MustScanSymLink(directory, symLinksToScan);
      var result = scan || directory.FileId == UInt128.Zero || directory.SymLinkFileIndices.Length > 0 ||
                   changed.Contains(directory);
      if (!scan) {
        foreach (var child in directory.ChildDirectories.ToForeachEnum()) {
          result |= MarkDirectoriesToVisit(child, changed, symLinksToScan, visit);
        }
      }
      if (result) {
        visit.Add(directory);
      }
      return result;
    }

    /// <summary>
    /// True for a directory symbolic link or junction whose subtree must be scanned entirely: its
    /// target is on another volume (no id), or changed.
    /// </summary>
    internal static bool MustScanSymLink(DirectorySnapshot directory, IReadOnlySet<DirectorySnapshot> symLinksToScan) {
      return directory.IsSymLink && (directory.FileId == UInt128.Zero || (symLinksToScan != null && symLinksToScan.Contains(directory)));
    }

    private Task<DirectorySnapshot> StartCatchUpDirectory(DirectorySnapshot directory, CatchUpContext context) {
      if (!context.Visit.Contains(directory))
        return Task.FromResult(directory);
      return Task.Factory.StartNew(
        () => CatchUpDirectoryAsync(directory, context),
        _cancellationToken,
        TaskCreationOptions.DenyChildAttach,
        _indexingWorkerPool.Scheduler).Unwrap();
    }

    private async Task<DirectorySnapshot> CatchUpDirectoryAsync(DirectorySnapshot directory, CatchUpContext context) {
      _cancellationToken.ThrowIfCancellationRequested();
      if (context.MustScan(directory)) {
        // The changes made through a directory symbolic link or a junction have records with the
        // ids of the target: scanned entirely if the target is on another volume, or changed.
        Interlocked.Increment(ref context.Statistics.SymLinkSubtreeCount);
        var data = directory.DirectoryData;
        return await ProcessDirectoryAsync(new DirectoryData(data.DirectoryName, true,
          GetDirectoryId(data.DirectoryName, true, UInt128.Zero)), _cancellationToken);
      }
      if (!context.Visit.Contains(directory))
        return directory;
      if (directory.FileId == UInt128.Zero || context.Changed.Contains(directory))
        return await RelistDirectoryAsync(directory, context);

      // The listing of the directory is unchanged: its file symbolic links (metadata of their
      // target) and its subdirectories.
      var metadata = directory.ChildFileMetadataArray;
      foreach (var index in directory.SymLinkFileIndices) {
        Interlocked.Increment(ref context.Statistics.SymLinkFileCount);
        var info = _fileSystem.GetFileInfoSnapshot(directory.ChildFiles[index].FullPath);
        if (!info.Exists || !info.IsFile) {
          // Not expected (the link would have a record): listed again.
          return await RelistDirectoryAsync(directory, context);
        }
        var newMetadata = new FileMetadata(info.LastWriteTimeUtc, info.Length);
        if (newMetadata != metadata[index]) {
          if (ReferenceEquals(metadata, directory.ChildFileMetadataArray)) {
            metadata = (FileMetadata[])metadata.Clone();
          }
          metadata[index] = newMetadata;
        }
      }

      var childTasks = new Task<DirectorySnapshot>[directory.ChildDirectories.Count];
      for (var i = 0; i < childTasks.Length; i++) {
        childTasks[i] = StartCatchUpDirectory(directory.ChildDirectories[i], context);
      }
      var children = await Task.WhenAll(childTasks);
      var childrenChanged = false;
      for (var i = 0; i < children.Length; i++) {
        childrenChanged |= !ReferenceEquals(children[i], directory.ChildDirectories[i]);
      }
      if (!childrenChanged && ReferenceEquals(metadata, directory.ChildFileMetadataArray))
        return directory;
      IList<DirectorySnapshot> childDirectories = childrenChanged
        ? children.Where(x => x != null).ToReadOnlyList()
        : directory.ChildDirectories;
      return new DirectorySnapshot(directory.DirectoryData, childDirectories, directory.ChildFiles, metadata,
        directory.SymLinkFileIndicesArray);
    }

    /// <summary>
    /// Lists a directory again: its files, the subdirectories of the tree with the same name and
    /// file id (caught up), and the new or replaced subdirectories (scanned entirely).
    /// </summary>
    private async Task<DirectorySnapshot> RelistDirectoryAsync(DirectorySnapshot directory, CatchUpContext context) {
      Interlocked.Increment(ref context.Statistics.ListedDirectoryCount);
      var entries = GetDirectoryEntries(directory.DirectoryData);
      Dictionary<string, DirectorySnapshot> oldChildren = null;
      if (directory.ChildDirectories.Count > 0) {
        oldChildren = new Dictionary<string, DirectorySnapshot>(SystemPathComparer.NameComparer);
        foreach (var child in directory.ChildDirectories.ToForeachEnum()) {
          oldChildren[child.DirectoryName.Name] = child;
        }
      }

      List<Task<DirectorySnapshot>> childTasks = null;
      var childFiles = new List<(FileName Name, FileMetadata Metadata, bool IsSymLink)>();
      for (var i = 0; i < entries.Count; i++) {
        var entry = entries[i];
        if (entry.IsDirectory) {
          Task<DirectorySnapshot> childTask;
          if (oldChildren != null && oldChildren.TryGetValue(entry.Name, out var oldChild) &&
              string.Equals(oldChild.DirectoryName.Name, entry.Name, StringComparison.Ordinal) &&
              oldChild.FileId != UInt128.Zero && oldChild.FileId == entry.FileId &&
              !oldChild.IsSymLink && !entry.IsSymLink) {
            // The same directory: its changes (if any) have their own records.
            childTask = StartCatchUpDirectory(oldChild, context);
          } else {
            // A new directory (or replaced, renamed, moved in): scanned entirely.
            Interlocked.Increment(ref context.Statistics.ScannedSubtreeCount);
            var name = _fileSystemNameFactory.CreateDirectoryName(directory.DirectoryName, entry.Name, entry.IsSymLink);
            childTask = TraverseDirectoryAsync(new DirectoryData(name, entry.IsSymLink, GetDirectoryId(name, entry.IsSymLink, entry.FileId)),
              _cancellationToken);
          }
          (childTasks ??= new List<Task<DirectorySnapshot>>()).Add(childTask);
        } else if (entry.IsFile) {
          childFiles.Add((_fileSystemNameFactory.CreateFileName(directory.DirectoryName, entry.Name), entry.Metadata, entry.IsSymLink));
        }
      }

      var (fileNames, fileMetadata, symLinkFiles) = FilterAndSortFiles(childFiles);
      IList<DirectorySnapshot> childDirectories;
      if (childTasks == null) {
        childDirectories = ArrayUtilities.EmptyList<DirectorySnapshot>.Instance;
      } else {
        var childSnapshots = await Task.WhenAll(childTasks);
        childDirectories = childSnapshots
          .Where(x => x != null)
          .OrderBy(x => x.DirectoryName.Name, SystemPathComparer.NameComparer)
          .ToReadOnlyList();
      }
      return new DirectorySnapshot(directory.DirectoryData, childDirectories, fileNames, fileMetadata, symLinkFiles);
    }
  }

  /// <summary>The work of a catch-up from the change journal (<see cref="ProjectRootSnapshotBuilder.CatchUp"/>).</summary>
  public sealed class CatchUpStatistics {
    /// <summary>The directories listed again (with records in the journal, or without file id).</summary>
    public int ListedDirectoryCount;
    /// <summary>The new or replaced directories scanned entirely.</summary>
    public int ScannedSubtreeCount;
    /// <summary>The directory symbolic links and junctions scanned entirely.</summary>
    public int SymLinkSubtreeCount;
    /// <summary>The file symbolic links checked again.</summary>
    public int SymLinkFileCount;
  }
}
