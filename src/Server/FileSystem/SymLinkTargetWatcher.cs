// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// Watches the targets of the symbolic links outside of the projects (the projects are watched by
  /// the directory watcher): the directory of the target of each file link (not recursive) and the
  /// target of each directory link (recursive), see <see cref="SymLinkTargetMap.GetTargetDirectoriesToWatch"/>.
  /// The changes are posted (after a short delay without new change) with the watched directory as
  /// base path: <see cref="SymLinkTargetMap.Expand"/> maps them to the paths of the links. At most
  /// <see cref="MaxWatchers"/> directories; directories on network drives are not watched (their
  /// links are refreshed by the next scan or catch-up). A watcher error (e.g. overflow) is logged
  /// and the watcher restarted: the changes it missed are found by the next scan or catch-up.
  /// </summary>
  public sealed class SymLinkTargetWatcher : IDisposable {
    public const int MaxWatchers = 512;
    private static readonly TimeSpan PostDelay = TimeSpan.FromMilliseconds(300);

    private readonly IFileSystem _fileSystem;
    private readonly Action<IList<PathChangeEntry>> _changesHandler;
    private readonly object _lock = new object();
    private readonly Dictionary<(FullPath Directory, bool Recursive), IFileSystemWatcher> _watchers =
      new Dictionary<(FullPath, bool), IFileSystemWatcher>();
    private readonly List<PathChangeEntry> _pending = new List<PathChangeEntry>();
    private readonly Timer _timer;
    private bool _capLogged;
    private bool _disposed;

    public SymLinkTargetWatcher(IFileSystem fileSystem, Action<IList<PathChangeEntry>> changesHandler) {
      _fileSystem = fileSystem;
      _changesHandler = changesHandler;
      _timer = new Timer(_ => Post(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>The watched directories (tests, diagnostics).</summary>
    public IReadOnlyList<(FullPath Directory, bool Recursive)> WatchedDirectories {
      get {
        lock (_lock) {
          return _watchers.Keys.ToList();
        }
      }
    }

    /// <summary>Watches <paramref name="directories"/> (and stops watching the others).</summary>
    public void Update(IReadOnlyList<(FullPath Directory, bool Recursive)> directories) {
      var wanted = directories.Where(x => IsWatchable(x.Directory)).ToList();
      if (wanted.Count > MaxWatchers) {
        if (!_capLogged) {
          _capLogged = true;
          Logger.LogWarn("{0:n0} directories of targets of symbolic links outside of the projects: only {1:n0} are watched, " +
                         "the changes of the others are found by the next scan or catch-up.", wanted.Count, MaxWatchers);
        }
        wanted = wanted.Take(MaxWatchers).ToList();
      }
      lock (_lock) {
        if (_disposed)
          return;
        var wantedSet = new HashSet<(FullPath, bool)>(wanted);
        foreach (var key in _watchers.Keys.Where(x => !wantedSet.Contains(x)).ToList()) {
          _watchers[key].Dispose();
          _watchers.Remove(key);
          Logger.LogInfo("Stopped watching the target directory of symbolic links \"{0}\".", key.Item1);
        }
        foreach (var key in wanted.Where(x => !_watchers.ContainsKey(x))) {
          var watcher = TryStart(key.Directory, key.Recursive);
          if (watcher != null) {
            _watchers.Add(key, watcher);
            Logger.LogInfo("Watching the target directory of symbolic links \"{0}\" ({1}).", key.Directory,
              key.Recursive ? "recursive" : "not recursive");
          }
        }
      }
    }

    private static bool IsWatchable(FullPath directory) {
      var path = directory.Value;
      if (path.StartsWith(@"\\", StringComparison.Ordinal))
        return false;
      try {
        var root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root) && OperatingSystem.IsWindows() && new DriveInfo(root).DriveType == DriveType.Network)
          return false;
        return Directory.Exists(path);
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) {
        return false;
      }
    }

    private IFileSystemWatcher TryStart(FullPath directory, bool recursive) {
      IFileSystemWatcher watcher = null;
      try {
        watcher = _fileSystem.CreateDirectoryWatcher(directory);
        watcher.IncludeSubdirectories = recursive;
        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size;
        // Note: The buffer of a pending notification is non-paged memory: small for the directories
        // of file link targets (few changes), larger for the subtrees of directory link targets.
        watcher.InternalBufferSize = recursive ? 64 * 1024 : 8 * 1024;
        watcher.Changed += (s, e) => OnChange(directory, e.Name, PathChangeKind.Changed);
        watcher.Created += (s, e) => OnChange(directory, e.Name, PathChangeKind.Created);
        watcher.Deleted += (s, e) => OnChange(directory, e.Name, PathChangeKind.Deleted);
        watcher.Renamed += (s, e) => {
          OnChange(directory, e.OldName, PathChangeKind.Deleted);
          OnChange(directory, e.Name, PathChangeKind.Created);
        };
        watcher.Error += (s, e) => OnError(directory, recursive, e.GetException());
        watcher.Start();
        return watcher;
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) {
        Logger.LogWarn(e, "Cannot watch the target directory of symbolic links \"{0}\".", directory);
        watcher?.Dispose();
        return null;
      }
    }

    private void OnChange(FullPath directory, string name, PathChangeKind kind) {
      if (string.IsNullOrEmpty(name) || !PathHelpers.IsValidBclPath(name))
        return;
      lock (_lock) {
        if (_disposed)
          return;
        _pending.Add(new PathChangeEntry(directory, new RelativePath(name), kind, PathKind.FileOrDirectory));
        _timer.Change(PostDelay, Timeout.InfiniteTimeSpan);
      }
    }

    private void OnError(FullPath directory, bool recursive, Exception error) {
      Logger.LogWarn(error, "Error watching the target directory of symbolic links \"{0}\": restarted (the changes missed " +
                            "are found by the next scan or catch-up).", directory);
      lock (_lock) {
        if (_disposed || !_watchers.TryGetValue((directory, recursive), out var watcher))
          return;
        watcher.Dispose();
        _watchers.Remove((directory, recursive));
        var restarted = TryStart(directory, recursive);
        if (restarted != null) {
          _watchers.Add((directory, recursive), restarted);
        }
      }
    }

    private void Post() {
      List<PathChangeEntry> changes;
      lock (_lock) {
        if (_disposed || _pending.Count == 0)
          return;
        changes = _pending.ToList();
        _pending.Clear();
      }
      Logger.WrapActionInvocation(() => _changesHandler(changes));
    }

    public void Dispose() {
      lock (_lock) {
        _disposed = true;
        foreach (var watcher in _watchers.Values) {
          watcher.Dispose();
        }
        _watchers.Clear();
      }
      _timer.Dispose();
    }
  }
}
