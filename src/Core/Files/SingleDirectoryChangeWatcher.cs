// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Watch a single directory for file change notifications.
  /// Changes are accumulated in an internal data structure until
  /// <see cref="DequeueChangedPathsEvents"/> is called. 
  /// </summary>
  public class SingleDirectoryChangeWatcher : IDisposable {
    /// <summary>
    /// Static collection that can be examined when using an interactive debugger.
    /// </summary>
    private static readonly ChangeLogQueue GlobalChangeLog = new ChangeLogQueue();

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly FullPath _directory;
    private readonly IFileSystemWatcher _watcher;
    private readonly PeriodicTimer _timer;
    private readonly BoundedOperationLimiter _logLimiter = new BoundedOperationLimiter(10);

    /// <summary>
    /// Dictionary of file change events, per path.
    /// </summary>
    private Dictionary<RelativePath, PathChangeKind> _changedPaths = new Dictionary<RelativePath, PathChangeKind>();
    private readonly object _changedPathsLock = new object();

    public SingleDirectoryChangeWatcher(IFileSystem fileSystem, IDateTimeProvider dateTimeProvider, FullPath directory) {
      _dateTimeProvider = dateTimeProvider;
      _directory = directory;

      _watcher = fileSystem.CreateDirectoryWatcher(directory);
      _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
      _watcher.IncludeSubdirectories = true;
      // Note: The MSDN documentation says to use less than 64KB
      //       (see https://msdn.microsoft.com/en-us/library/system.io.filesystemwatcher.internalbuffersize(v=vs.110).aspx)
      //         "You can set the buffer to 4 KB or larger, but it must not exceed 64 KB."
      //       However, the implementation allows for arbitrary buffer sizes.
      //       Experience has shown that 64KB is small enough that we frequently run into "OverflowException"
      //       exceptions on heavily active file systems (e.g. during a build of a complex project
      //       such as Chromium).
      //       The issue with these exceptions is that the consumer must be extremely conservative
      //       when such errors occur, because we lost track of what happened at the individual
      //       directory/file level. In the case of VsChromium, the server will batch a full re-scan
      //       of the file system, instead of an incremental re-scan, and that can be quite time
      //       consuming (as well as I/O consuming).
      //       In the end, increasing the size of the buffer to 2 MB is the best option to avoid
      //       these issues (2 MB is not that much memory in the grand scheme of things).
      _watcher.InternalBufferSize = 2 * 1024 * 1024; // 2 MB
      _watcher.Changed += WatcherOnChanged;
      _watcher.Created += WatcherOnCreated;
      _watcher.Deleted += WatcherOnDeleted;
      _watcher.Renamed += WatcherOnRenamed;
      _watcher.Error += WatcherOnError;

      _timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
      _timer.Elapsed += TimerOnElapsed;
    }

    public event EventHandler<IList<KeyValuePair<RelativePath, PathChangeKind>>> PathsChanged;
    public event EventHandler<Exception> Error;

    public FullPath DirectoryPath {
      get { return _directory; }
    }

    public void Start() {
      Logger.LogInfo("Starting monitoring directory \"{0}\" for change notifications.", _directory);
      _watcher.Start();
      _timer.Start();
    }

    public void Dispose() {
      Logger.LogInfo("Removing directory \"{0}\" from change notification monitoring.", _directory);
      _timer.Dispose();
      _watcher.Dispose();
    }

    private void WatcherOnError(object sender, ErrorEventArgs errorEventArgs) {
      Logger.WrapActionInvocation(() => {
        Logger.LogError(errorEventArgs.GetException(), "File system watcher for path \"{0}\" error.",
          _directory);
        OnError(errorEventArgs.GetException());
      });
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs args) {
      Logger.WrapActionInvocation(() => {
        // Check we can handle path argument
        if (SkipRelativePath(args.Name))
          return;

        EnqueueChangeEvent(new RelativePath(args.Name), PathChangeKind.Created);
      });
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs args) {
      Logger.WrapActionInvocation(() => {
        // Check we can handle path argument
        if (SkipRelativePath(args.Name))
          return;

        EnqueueChangeEvent(new RelativePath(args.Name), PathChangeKind.Deleted);
      });
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs args) {
      Logger.WrapActionInvocation(() => {
        // Check we can handle path argument
        if (SkipRelativePath(args.Name))
          return;

        EnqueueChangeEvent(new RelativePath(args.Name), PathChangeKind.Changed);
      });
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs args) {
      Logger.WrapActionInvocation(() => {
        // Check we can handle path arguments
        if (SkipRelativePath(args.Name))
          return;
        if (SkipRelativePath(args.OldName))
          return;

        EnqueueChangeEvent(new RelativePath(args.OldName), PathChangeKind.Deleted);
        EnqueueChangeEvent(new RelativePath(args.Name), PathChangeKind.Created);
      });
    }

    /// <summary>
    /// Fire the <see cref="PathsChanged"/> periodically as long as the list of changed
    /// paths is not empty.
    /// </summary>
    private void TimerOnElapsed(object sender, EventArgs eventArgs) {
      // Since this event comes from the timer, we knows there must be some path changes
      // enqeued.
      var changes = DequeueChangedPathsEvents();
      if (changes.Count > 0) {
        OnPathsChanged(changes.ToList());
      }
    }

    /// <summary>
    /// This method is called on the file watcher callback thread. It should
    /// be as efficient as possible so that the internal file watcher buffer
    /// does not overflow.
    /// </summary>
    private void EnqueueChangeEvent(RelativePath entryPath, PathChangeKind changeKind) {
      LogRelativePath(entryPath, changeKind);
      GlobalChangeLog.LogLastChange(_directory, entryPath, changeKind, _dateTimeProvider.UtcNow);

      // Combine change kinds with the given relative path
      lock (_changedPathsLock) {
        // Retrieve existing change kind
        PathChangeKind previousChangeKind;
        if (!_changedPaths.TryGetValue(entryPath, out previousChangeKind)) {
          previousChangeKind = PathChangeKind.None;
        }

        // Merge change kinds
        var newChangeKind = CombineChangeKinds(previousChangeKind, changeKind);

        // Update table with new change kind
        if (newChangeKind == PathChangeKind.None) {
          // Remove entry for files that have been created then deleted
          _changedPaths.Remove(entryPath);
        } else {
          // Update entry for other cases
          _changedPaths[entryPath] = newChangeKind;
        }
      }
    }

    private IDictionary<RelativePath, PathChangeKind> DequeueChangedPathsEvents() {
      // Copy current changes into temp and reset to empty collection.
      lock (_changedPathsLock) {
        var temp = _changedPaths;
        _changedPaths = new Dictionary<RelativePath, PathChangeKind>();
        return temp;
      }
    }

    public static PathChangeKind CombineChangeKinds(PathChangeKind current, PathChangeKind next) {
      switch (current) {
        case PathChangeKind.None:
          return next;
        case PathChangeKind.Created:
          switch (next) {
            case PathChangeKind.None:
              return current;
            case PathChangeKind.Created:
              return current;
            case PathChangeKind.Deleted:
              return PathChangeKind.None;
            case PathChangeKind.Changed:
              return current;
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        case PathChangeKind.Deleted:
          switch (next) {
            case PathChangeKind.None:
              return current;
            case PathChangeKind.Created:
              return PathChangeKind.Changed;
            case PathChangeKind.Deleted:
              return current;
            case PathChangeKind.Changed:
              return PathChangeKind.Deleted; // Weird case...
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        case PathChangeKind.Changed:
          switch (next) {
            case PathChangeKind.None:
              return current;
            case PathChangeKind.Created:
              return PathChangeKind.Changed; // Weird case...
            case PathChangeKind.Deleted:
              return next;
            case PathChangeKind.Changed:
              return current;
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        default:
          throw new ArgumentOutOfRangeException("current");
      }
    }

    /// <summary>
    ///  Skip paths BCL can't process (e.g. path too long)
    /// </summary>
    private bool SkipRelativePath(string relativePath) {
      var path = PathHelpers.CombinePaths(_directory.Value, relativePath);
      if (PathHelpers.IsPathTooLong(path)) {
        switch (_logLimiter.Proceed()) {
          case BoundedOperationLimiter.Result.YesAndLast:
            Logger.LogInfo("(The following log message will be the last of its kind)", relativePath);
            goto case BoundedOperationLimiter.Result.Yes;
          case BoundedOperationLimiter.Result.Yes:
            Logger.LogInfo("Skipping file change event because path is too long: \"{0}\"", relativePath);
            break;
          case BoundedOperationLimiter.Result.NoMore:
            break;
        }
        return true;
      }
      if (!PathHelpers.IsValidBclPath(path)) {
        switch (_logLimiter.Proceed()) {
          case BoundedOperationLimiter.Result.YesAndLast:
            Logger.LogInfo("(The following log message will be the last of its kind)", relativePath);
            goto case BoundedOperationLimiter.Result.Yes;
          case BoundedOperationLimiter.Result.Yes:
            Logger.LogInfo("Skipping file change event because path is invalid: \"{0}\"", relativePath);
            break;
          case BoundedOperationLimiter.Result.NoMore:
            break;
        }
        return true;
      }
      return false;
    }

    protected virtual void OnPathsChanged(IList<KeyValuePair<RelativePath, PathChangeKind>> changes) {
      PathsChanged?.Invoke(this, changes);
    }

    protected virtual void OnError(Exception obj) {
      Error?.Invoke(this, obj);
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private static void LogRelativePath(RelativePath relativePath, PathChangeKind kind) {
#if false
      var pathToLog = @"";
      if (SystemPathComparer.Instance.IndexOf(path, pathToLog, 0, path.Length) == 0) {
        Logger.LogInfo("*************************** {0}: {1} *******************", path, kind);
      }
#endif
    }

    public class ChangeLogQueue {
      private readonly Queue<ChangeLog> _lastChanges = new Queue<ChangeLog>();

      public void LogLastChange(FullPath directory, RelativePath entryPath, PathChangeKind changeKind, DateTime time) {
        var entry = new ChangeLog {
          DirectoryPath = directory,
          EntryPath = entryPath,
          ChangeKind = changeKind,
          TimeStampUtc = time,
        };
        lock (((ICollection)_lastChanges).SyncRoot) {
          if (_lastChanges.Count >= 100) {
            _lastChanges.Dequeue();
          }
          _lastChanges.Enqueue(entry);
        }
      }

      public class ChangeLog {
        public FullPath DirectoryPath { get; set; }
        public RelativePath EntryPath { get; set; }
        public PathChangeKind ChangeKind { get; set; }
        public DateTime TimeStampUtc { get; set; }
      }
    }
  }
}
