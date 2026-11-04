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
    /// Writes the cache of the projects of <paramref name="snapshot"/> in the background (low
    /// priority), after the pending writes. <paramref name="loadStartUtc"/> is the time the full
    /// load started (see <see cref="ProjectIndexCache.RacyInterval"/>).
    /// </summary>
    void ScheduleWrite(FileSystemSnapshot snapshot, IFileDatabaseSnapshot database, DateTime loadStartUtc);

    /// <summary>Cancels the pending and running writes (e.g. a new load starts).</summary>
    void CancelWrites();

    /// <summary>
    /// Waits for the pending writes (tests). Returns false if they are not done after
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
    /// <summary>A write of the cache is queued or running.</summary>
    public bool Saving;
    /// <summary>The last write time of the index of the open project caches (the most recent), if any.</summary>
    public DateTime? LastSavedUtc;
    /// <summary>The oldest index of the caches written by this server (null if one has no index yet).</summary>
    public DateTime? OldestWritableSavedUtc;
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
    private CancellationTokenSource _writeCancellation = new CancellationTokenSource();
    private Task _writeTask = Task.CompletedTask;
    private bool _disposed;
    private readonly IndexCacheStatistics _statistics = new IndexCacheStatistics();

    public IndexCache(IndexCacheOptions options) {
      _options = options;
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

    public void ScheduleWrite(FileSystemSnapshot snapshot, IFileDatabaseSnapshot database, DateTime loadStartUtc) {
      if (!IsEnabled || database is not FileDatabaseSnapshot fileDatabase)
        return;
      lock (_lock) {
        if (_disposed)
          return;
        _writeCancellation.Cancel();
        var cancellation = new CancellationTokenSource();
        _writeCancellation = cancellation;
        _writeTask = _writeTask.ContinueWith(_ => RunWrite(snapshot, fileDatabase, loadStartUtc, cancellation.Token),
          CancellationToken.None, TaskContinuationOptions.LongRunning, TaskScheduler.Default);
      }
    }

    public void CancelWrites() {
      lock (_lock) {
        _writeCancellation.Cancel();
      }
    }

    public bool WaitForWrites(TimeSpan timeout) {
      Task task;
      lock (_lock) {
        task = _writeTask;
      }
      return task.Wait(timeout);
    }

    private void RunWrite(FileSystemSnapshot snapshot, FileDatabaseSnapshot database, DateTime loadStartUtc,
                          CancellationToken cancellationToken) {
      if (cancellationToken.IsCancellationRequested)
        return;
      // Note: Must not slow down the searches and the indexing.
      Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
      try {
        foreach (var projectRoot in snapshot.ProjectRoots) {
          var cache = GetOpenProjectCache(projectRoot.Project.RootPath);
          if (cache == null || !cache.IsWritable)
            continue;
          var result = cache.Write(GetWriteDirectories(projectRoot, database), projectRoot.Project.VersionHash,
            loadStartUtc, cancellationToken);
          if (result == null)
            continue;
          Logger.LogInfo("Wrote the index cache of \"{0}\" in {1:n0} ms: {2:n0} entries, {3:n0} new files " +
                         "({4:n0} bytes written, {5:n0} new arena files, {6:n0} deleted), skipped {7:n0} files " +
                         "modified during the load, {8:n0} files modified after the scan, {9:n0} large files.",
            projectRoot.Project.RootPath, result.Elapsed.TotalMilliseconds, result.EntryCount, result.NewFileCount,
            result.BytesWritten, result.NewArenaCount, result.DeletedArenaCount, result.RacyFileCount,
            result.ChangedFileCount, result.LargeFileCount);
          if (result.ChangedFileSamples.Count > 0) {
            Logger.LogInfo("Files of \"{0}\" modified after the scan (scan time != contents time), first {1:n0} of {2:n0}:\n  {3}",
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
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error writing the index cache.");
      }
      finally {
        Thread.CurrentThread.Priority = ThreadPriority.Normal;
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
        result.Saving = !_writeTask.IsCompleted;
        projects = _projects.Values.Where(x => x != null).ToArray();
      }
      var oldestWritable = DateTime.MaxValue;
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
            // Note: A project without index yet is the oldest.
            var value = saved ?? DateTime.MinValue;
            if (value < oldestWritable)
              oldestWritable = value;
          }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
          // The directory may be deleted (e.g. "Rebuild index"): ignored.
        }
      }
      if (result.HasWritableProject && oldestWritable != DateTime.MinValue) {
        result.OldestWritableSavedUtc = oldestWritable;
      }
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

    public void Dispose() {
      Task task;
      lock (_lock) {
        _disposed = true;
        _writeCancellation.Cancel();
        task = _writeTask;
      }
      try {
        if (!task.Wait(TimeSpan.FromSeconds(10))) {
          Logger.LogWarn("Timeout waiting for the index cache write to be cancelled.");
        }
      }
      catch (AggregateException) {
      }
      lock (_lock) {
        foreach (var project in _projects.Values) {
          project?.Dispose();
        }
        _projects.Clear();
      }
    }
  }
}
