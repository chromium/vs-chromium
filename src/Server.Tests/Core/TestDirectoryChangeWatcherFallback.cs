// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;
using VsChromium.Core.Threads;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestDirectoryChangeWatcherFallback {
    [TestMethod]
    public void IsWatchLimitErrorWorks() {
      // Message of the .NET FileSystemWatcher on Linux when "fs.inotify.max_user_watches" is reached.
      Assert.IsTrue(DirectoryChangeWatcher.IsWatchLimitError(new IOException(
        "The configured user limit (8192) on the number of inotify watches has been reached, or the " +
        "operating system failed to allocate a required resource.")));
      Assert.IsTrue(DirectoryChangeWatcher.IsWatchLimitError(new Exception("outer",
        new IOException("The configured user limit (128) on the number of inotify instances has been reached"))));
      Assert.IsFalse(DirectoryChangeWatcher.IsWatchLimitError(new InternalBufferOverflowException("Too many changes at once")));
      Assert.IsFalse(DirectoryChangeWatcher.IsWatchLimitError(null));
    }

    [TestMethod]
    public void WatchLimitErrorFallsBackToPeriodicRescans() {
      var fileSystem = new FakeFileSystem();
      var watcher = new DirectoryChangeWatcher(fileSystem, new DateTimeProvider(), TimeSpan.FromSeconds(60)) {
        PollingFallbackPeriod = TimeSpan.FromMilliseconds(100),
      };
      var rescans = 0;
      var errors = 0;
      watcher.RescanRequired += () => Interlocked.Increment(ref rescans);
      watcher.Error += e => Interlocked.Increment(ref errors);

      var directory = new FullPath(Path.GetTempPath());
      watcher.WatchDirectories(new[] { directory });
      Assert.IsTrue(fileSystem.Watchers.Count >= 1);
      Assert.IsTrue(fileSystem.Watchers.TrueForAll(x => x.IsStarted));

      // Simulate the inotify watch limit error.
      fileSystem.Watchers[0].RaiseError(new IOException(
        "The configured user limit (8192) on the number of inotify watches has been reached, or the " +
        "operating system failed to allocate a required resource."));

      // The watchers are stopped, no error is reported (the index stays usable) and rescans are
      // requested periodically (the polling thread runs every second).
      var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
      while (Volatile.Read(ref rescans) < 2 && DateTime.UtcNow < deadline) {
        Thread.Sleep(50);
      }
      Assert.IsTrue(Volatile.Read(ref rescans) >= 2, "Expected periodic rescan requests");
      Assert.AreEqual(0, Volatile.Read(ref errors));
      Assert.IsTrue(fileSystem.Watchers.TrueForAll(x => !x.IsStarted));
    }

    private class FakeFileSystem : IFileSystem {
      public List<FakeWatcher> Watchers { get; } = new List<FakeWatcher>();

      public IFileSystemWatcher CreateDirectoryWatcher(FullPath path) {
        var result = new FakeWatcher(path);
        Watchers.Add(result);
        return result;
      }

      public IFileInfoSnapshot GetFileInfoSnapshot(FullPath path) => throw new NotImplementedException();
      public string ReadText(FullPath path) => throw new NotImplementedException();
      public SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, long fileSize, int trailingByteCount) => throw new NotImplementedException();
      public IList<DirectoryEntry> GetDirectoryEntries(FullPath path) => throw new NotImplementedException();
    }

    private class FakeWatcher : IFileSystemWatcher {
      public FakeWatcher(FullPath path) {
        Path = path;
      }

      public bool IsStarted { get; private set; }
      public FullPath Path { get; }
      public NotifyFilters NotifyFilter { get; set; }
      public int InternalBufferSize { get; set; }
      public bool IncludeSubdirectories { get; set; }

#pragma warning disable CS0067 // Event never used
      public event FileSystemEventHandler Changed;
      public event FileSystemEventHandler Created;
      public event FileSystemEventHandler Deleted;
      public event RenamedEventHandler Renamed;
#pragma warning restore CS0067
      public event ErrorEventHandler Error;

      public void RaiseError(Exception e) {
        Error?.Invoke(this, new ErrorEventArgs(e));
      }

      public void Start() {
        IsStarted = true;
      }

      public void Stop() {
        IsStarted = false;
      }

      public void Dispose() {
        IsStarted = false;
      }
    }
  }
}
