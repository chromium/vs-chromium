// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

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

    event EventHandler<FileSystemWatchStoppedEventArgs> FileSystemWatchStopped;
  }

  public class FileSystemWatchStoppedEventArgs : EventArgs {
    public bool IsError { get; set; }
  }
}
