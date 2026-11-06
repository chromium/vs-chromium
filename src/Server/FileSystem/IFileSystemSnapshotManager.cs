// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;
using System;
using VsChromium.Server.Operations;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// Keeps track of file system activity to expose a valid <see cref="FileSystemSnapshot"/>
  /// </summary>
  public interface IFileSystemSnapshotManager {
    /// <summary>
    /// Return the last fully scanned <see cref="FileSystemSnapshot"/>.
    /// If scan has not finished yet, or if there are no registered files,
    /// the resulting snapshot is empty (but not <code>null</code>).
    /// </summary>
    FileSystemSnapshot CurrentSnapshot { get; }

    void Pause();
    void Resume();
    void Refresh();

    /// <summary>
    /// Rescans the project whose root is <paramref name="projectRoot"/> and reloads all its files
    /// ("project/rebuild"): the other projects keep their tree and contents.
    /// </summary>
    void RebuildProject(FullPath projectRoot);

    /// <summary>
    /// Enables or disables the watchers of the targets of the symbolic links of the project whose
    /// root is <paramref name="projectRoot"/> outside of the project roots (disabled by default; see
    /// <see cref="SymLinkTargetWatcher"/>). Kept for the lifetime of the server (also for a project
    /// not loaded yet).
    /// </summary>
    void SetWatchExternalLinkTargets(FullPath projectRoot, bool enabled) { }

    /// <summary>The symbolic links of a project and the watchers of their targets (no disk access).</summary>
    SymLinkStatistics GetSymLinkStatistics(FullPath projectRoot) => new SymLinkStatistics();

    /// <summary>
    /// The symbolic links of the current snapshot by target (built in the background after each
    /// scan: may lag behind <see cref="CurrentSnapshot"/>).
    /// </summary>
    SymLinkTargetMap SymLinkTargets => SymLinkTargetMap.Empty;

    /// <summary>
    /// Event raised when a file system scan operation has started
    /// </summary>
    event EventHandler<OperationInfo> SnapshotScanStarted;

    /// <summary>
    /// Event raised when a file system scan operation has terminated
    /// successfully or due to an error (including cancellation).
    /// </summary>
    event EventHandler<SnapshotScanResult> SnapshotScanFinished;

    /// <summary>
    /// Event raised when files have changed on disk while the <see cref="CurrentSnapshot"/>
    /// remains unchanged.
    /// </summary>
    event EventHandler<FilesChangedEventArgs> FilesChanged;

    event EventHandler<FileSystemWatchPausedEventArgs> FileSystemWatchPaused;
    event EventHandler FileSystemWatchResumed;
  }

  public class FileSystemWatchPausedEventArgs : EventArgs {
    public bool IsError { get; set; }
  }
}
