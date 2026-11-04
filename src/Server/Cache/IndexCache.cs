// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Files;
using Microsoft.Win32.SafeHandles;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;

namespace VsChromium.Server.Cache {
  /// <summary>
  /// The persistent index cache (phase 1: cached file contents, validated by the scan): the
  /// contents of the files of each project are written to a cache directory after a full load, and
  /// the next full loads (e.g. after a restart of the server) use the cached contents of the files
  /// whose time and size are unchanged instead of reading them.
  /// </summary>
  public interface IIndexCache {
    bool IsEnabled { get; }

    /// <summary>
    /// The cache of a project (opened on first use), or null if the cache is disabled or can't be
    /// opened.
    /// </summary>
    ProjectIndexCache GetProjectCache(FullPath rootPath);

    /// <summary>Records the number of files of a full load read from the cache and from disk.</summary>
    void RecordLoad(int filesFromCache, int filesFromDisk);

    /// <summary>
    /// The index was updated (any update: full load, tree changes, edits): the cache is saved
    /// later (<see cref="IndexCacheSaveScheduler"/>: once the changes settle, at most 5 minutes
    /// after the first unsaved change), or as soon as possible after a full load of a project
    /// without saved cache (e.g. the first load, or after "Rebuild index").
    /// </summary>
    void NotifyIndexUpdated(FileSystemSnapshot snapshot, IFileDatabaseSnapshot database, bool fullLoad);

    /// <summary>Cancels the running write, if any (e.g. a new full load starts): saved later.</summary>
    void CancelWrites();

    /// <summary>
    /// Waits for the running write (tests, invalidation). Returns false if it is not done after
    /// <paramref name="timeout"/>.
    /// </summary>
    bool WaitForWrites(TimeSpan timeout);

    /// <summary>Deletes the cache of projects ("Rebuild index").</summary>
    void Invalidate(IEnumerable<FullPath> rootPaths);

    /// <summary>
    /// True if <paramref name="contents"/> were loaded from the cache of a project, and the cache
    /// was deleted since (<see cref="Invalidate"/>): they must not be reused by the next load.
    /// </summary>
    bool IsFromInvalidatedCache(FileContents contents);

    IndexCacheStatistics GetStatistics();

    /// <summary>
    /// The state of the cache on disk (status displays), from existing state only: the pending
    /// writes, and the files of the caches of the open projects (enumerated: a few hundred files).
    /// </summary>
    IndexCacheDiskState GetDiskState();
  }

  public sealed class IndexCacheDiskState {
    public bool Enabled;
    /// <summary>A write of the cache is running.</summary>
    public bool Saving;
    /// <summary>The index changed since the last write (started) of the caches written by this server.</summary>
    public bool Dirty;
    /// <summary>The last write time of the index of the open project caches (the most recent), if any.</summary>
    public DateTime? LastSavedUtc;
    /// <summary>True if this server writes the cache of at least one open project.</summary>
    public bool HasWritableProject;
    public long SizeOnDiskBytes;
  }

  public sealed class IndexCacheStatistics {
    public bool Enabled;
    public string CacheDirectory;
    public int ProjectCount;
    public int ReadOnlyProjectCount;
    public int LastLoadFilesFromCache;
    public int LastLoadFilesFromDisk;
    public int WriteCount;
    public double LastWriteMs;
    public long LastWriteBytes;
    public int LastWriteEntryCount;
    public int LastWriteNewFileCount;
    public int LastWriteChangedFileCount;
  }

  public sealed class IndexCache : IIndexCache, IDisposable {
    private readonly IndexCacheOptions _options;
    private readonly object _lock = new object();
    private readonly Dictionary<FullPath, ProjectIndexCache> _projects = new Dictionary<FullPath, ProjectIndexCache>();
    private readonly IndexCacheSaveScheduler _scheduler;
    /// <summary>The state to save (the last update), until a save starts.</summary>
    private FileSystemSnapshot _pendingSnapshot;
    private FileDatabaseSnapshot _pendingDatabase;
    private bool _disposed;
    private readonly IndexCacheStatistics _statistics = new IndexCacheStatistics();

    public IndexCache(IndexCacheOptions options) {
      _options = options;
      _scheduler = new IndexCacheSaveScheduler(options.SaveSettleDelay, options.SaveMaxDelay, Save);
      if (options.IsEnabled) {
        Logger.LogInfo("Index cache directory: \"{0}\"", options.CacheDirectory);
      } else {
        Logger.LogInfo("Index cache disabled");
      }
    }

    public bool IsEnabled => _options.IsEnabled;

    public ProjectIndexCache GetProjectCache(FullPath rootPath) {
      if (!IsEnabled)
        return null;
      lock (_lock) {
        if (_disposed)
          return null;
        if (_projects.TryGetValue(rootPath, out var result))
          return result;
        try {
          result = new ProjectIndexCache(_options.CacheDirectory, rootPath.Value);
          result.Open();
        }
        catch (Exception e) {
          Logger.LogWarn(e, "Cannot open the index cache of \"{0}\": not used.", rootPath);
          result?.Dispose();
          result = null;
        }
        // Note: Not retried if it failed.
        _projects.Add(rootPath, result);
        return result;
      }
    }

    private ProjectIndexCache GetOpenProjectCache(FullPath rootPath) {
      lock (_lock) {
        return _projects.TryGetValue(rootPath, out var result) ? result : null;
      }
    }

    public void RecordLoad(int filesFromCache, int filesFromDisk) {
      lock (_statistics) {
        _statistics.LastLoadFilesFromCache = filesFromCache;
        _statistics.LastLoadFilesFromDisk = filesFromDisk;
      }
    }

    public void NotifyIndexUpdated(FileSystemSnapshot snapshot, IFileDatabaseSnapshot database, bool fullLoad) {
      if (!IsEnabled || database is not FileDatabaseSnapshot fileDatabase)
        return;
      // Only the projects whose cache is written by this server.
      var caches = snapshot.ProjectRoots
        .Select(x => GetOpenProjectCache(x.Project.RootPath))
        .Where(x => x != null && x.IsWritable)
        .ToList();
      if (caches.Count == 0)
        return;
      // The cache must exist soon after the first load of a project.
      var immediate = fullLoad && caches.Any(x => !x.HasIndex);
      lock (_lock) {
        if (_disposed)
          return;
        _pendingSnapshot = snapshot;
        _pendingDatabase = fileDatabase;
      }
      _scheduler.NotifyChanged(immediate);
    }

    public void CancelWrites() {
      _scheduler.CancelSave();
    }

    public bool WaitForWrites(TimeSpan timeout) {
      return _scheduler.WaitForSave(timeout);
    }

    /// <summary>Saves the state of the last update (on the thread of the scheduler).</summary>
    private void Save(CancellationToken cancellationToken) {
      FileSystemSnapshot snapshot;
      FileDatabaseSnapshot database;
      lock (_lock) {
        snapshot = _pendingSnapshot;
        database = _pendingDatabase;
        _pendingSnapshot = null;
        _pendingDatabase = null;
      }
      if (snapshot == null)
        return;
      try {
        RunWrite(snapshot, database, cancellationToken);
      }
      catch (OperationCanceledException) {
        lock (_lock) {
          // Saved later (unless a newer update replaced it).
          _pendingSnapshot ??= snapshot;
          _pendingDatabase ??= database;
        }
        throw;
      }
    }

    private void RunWrite(FileSystemSnapshot snapshot, FileDatabaseSnapshot database, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        foreach (var projectRoot in snapshot.ProjectRoots) {
          var cache = GetOpenProjectCache(projectRoot.Project.RootPath);
          if (cache == null || !cache.IsWritable)
            continue;
          var result = cache.Write(GetWriteDirectories(projectRoot, database), projectRoot.Project.VersionHash,
            cancellationToken);
          if (result == null)
            continue;
          Logger.LogInfo("Wrote the index cache of \"{0}\" in {1:n0} ms: {2:n0} entries, {3:n0} new files " +
                         "({4:n0} bytes written, {5:n0} new arena files, {6:n0} deleted), skipped {7:n0} files " +
                         "modified less than 2 s before they were read, {8:n0} files read with another time or size than the scan, {9:n0} large files.",
            projectRoot.Project.RootPath, result.Elapsed.TotalMilliseconds, result.EntryCount, result.NewFileCount,
            result.BytesWritten, result.NewArenaCount, result.DeletedArenaCount, result.RacyFileCount,
            result.ChangedFileCount, result.LargeFileCount);
          if (result.ChangedFileSamples.Count > 0) {
            Logger.LogInfo("Files of \"{0}\" read with another time or size than the scan, first {1:n0} of {2:n0}:\n  {3}",
              projectRoot.Project.RootPath, result.ChangedFileSamples.Count, result.ChangedFileCount,
              string.Join("\n  ", result.ChangedFileSamples));
          }
          lock (_statistics) {
            _statistics.WriteCount++;
            _statistics.LastWriteMs = result.Elapsed.TotalMilliseconds;
            _statistics.LastWriteBytes = result.BytesWritten;
            _statistics.LastWriteEntryCount = result.EntryCount;
            _statistics.LastWriteNewFileCount = result.NewFileCount;
            _statistics.LastWriteChangedFileCount = result.ChangedFileCount;
          }
        }
      }
      catch (OperationCanceledException) {
        Logger.LogInfo("Writing the index cache was cancelled.");
        throw;
      }
    }

    /// <summary>
    /// The tree of a project (directories in depth first order, parents first) with the metadata
    /// of the files (from the scan) and their contents (in the database, null if not searchable).
    /// </summary>
    private static IEnumerable<IndexCacheWriteDirectory> GetWriteDirectories(ProjectRootSnapshot projectRoot, FileDatabaseSnapshot database) {
      var stack = new Stack<DirectorySnapshot>();
      stack.Push(projectRoot.Directory);
      while (stack.Count > 0) {
        var directory = stack.Pop();
        var files = directory.ChildFiles;
        var metadata = directory.ChildFileMetadataArray;
        var items = new IndexCacheWriteItem[files.Count];
        for (var i = 0; i < files.Count; i++) {
          var fileName = files[i];
          var contents = database.TryGetFileIndex(fileName, out var index) ? database.GetContents(index) : null;
          items[i] = new IndexCacheWriteItem(fileName.Name, metadata[i], contents);
        }
        // Note: One string per directory (the files use their name).
        yield return new IndexCacheWriteDirectory(directory.DirectoryName.RelativePath.Value ?? "", directory.IsSymLink, items);
        for (var i = directory.ChildDirectories.Count - 1; i >= 0; i--) {
          stack.Push(directory.ChildDirectories[i]);
        }
      }
    }

    public void Invalidate(IEnumerable<FullPath> rootPaths) {
      if (!IsEnabled)
        return;
      CancelWrites();
      if (!WaitForWrites(TimeSpan.FromSeconds(30))) {
        Logger.LogWarn("Timeout waiting for the index cache write to be cancelled.");
      }
      foreach (var rootPath in rootPaths) {
        var cache = GetProjectCache(rootPath);
        if (cache != null) {
          Logger.LogInfo("Deleting the index cache of \"{0}\".", rootPath);
          cache.Invalidate();
        }
      }
    }

    public bool IsFromInvalidatedCache(FileContents contents) {
      if (contents?.Contents.Block is not SafeMemoryMappedViewHandle view)
        return false;
      ProjectIndexCache[] projects;
      lock (_lock) {
        projects = _projects.Values.Where(x => x != null).ToArray();
      }
      return !projects.Any(x => x.IsMappedArena(view));
    }

    public IndexCacheDiskState GetDiskState() {
      var result = new IndexCacheDiskState { Enabled = IsEnabled };
      if (!IsEnabled)
        return result;
      ProjectIndexCache[] projects;
      lock (_lock) {
        projects = _projects.Values.Where(x => x != null).ToArray();
      }
      result.Saving = _scheduler.IsSaving;
      foreach (var project in projects) {
        try {
          foreach (var file in new DirectoryInfo(project.Directory).EnumerateFiles("*", SearchOption.AllDirectories)) {
            result.SizeOnDiskBytes += file.Length;
          }
          var index = new FileInfo(Path.Combine(project.Directory, IndexCacheFile.IndexFileName));
          DateTime? saved = index.Exists ? index.LastWriteTimeUtc : null;
          if (saved.HasValue && (!result.LastSavedUtc.HasValue || saved.Value > result.LastSavedUtc.Value)) {
            result.LastSavedUtc = saved;
          }
          if (project.IsWritable) {
            result.HasWritableProject = true;
          }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
          // The directory may be deleted (e.g. "Rebuild index"): ignored.
        }
      }
      // Note: Updates only mark the index dirty if this server writes the cache of a project.
      result.Dirty = result.HasWritableProject && _scheduler.IsDirty;
      return result;
    }

    public IndexCacheStatistics GetStatistics() {
      var result = new IndexCacheStatistics();
      lock (_statistics) {
        result.LastLoadFilesFromCache = _statistics.LastLoadFilesFromCache;
        result.LastLoadFilesFromDisk = _statistics.LastLoadFilesFromDisk;
        result.WriteCount = _statistics.WriteCount;
        result.LastWriteMs = _statistics.LastWriteMs;
        result.LastWriteBytes = _statistics.LastWriteBytes;
        result.LastWriteEntryCount = _statistics.LastWriteEntryCount;
        result.LastWriteNewFileCount = _statistics.LastWriteNewFileCount;
        result.LastWriteChangedFileCount = _statistics.LastWriteChangedFileCount;
      }
      result.Enabled = IsEnabled;
      result.CacheDirectory = _options.CacheDirectory;
      lock (_lock) {
        var projects = _projects.Values.Where(x => x != null).ToList();
        result.ProjectCount = projects.Count;
        result.ReadOnlyProjectCount = projects.Count(x => !x.IsWritable);
      }
      return result;
    }

    /// <summary>
    /// No save at shutdown (closing the client must not be delayed): a running write is cancelled
    /// and not waited for (its thread is a background thread: the process can exit). The previous
    /// index stays valid (written to a temporary file, then renamed); the unreferenced arena files of
    /// an interrupted write are deleted the next time the cache is opened. The changes since the last
    /// save are re-read by the verification of the tree at the next start.
    /// </summary>
    public void Dispose() {
      _scheduler.Dispose();
      lock (_lock) {
        _disposed = true;
        _pendingSnapshot = null;
        _pendingDatabase = null;
        foreach (var project in _projects.Values) {
          project?.Dispose();
        }
        _projects.Clear();
      }
    }
  }
}
