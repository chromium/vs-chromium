// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemScanSnapshot;
using VsChromium.Server.Operations;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// Keeps track of file registration and creates/deletes <see cref="FileSystemSnapshot"/>
  /// instances as file system activity occurs.
  /// </summary>
  public interface IFileSystemSnapshotManager {
    /// <summary>
    /// Force a file system rescan
    /// </summary>
    void Refresh();

    /// <summary>
    /// Register a new file to serve as the base for figuring out project roots
    /// </summary>
    void RegisterFile(FullPath path);
    /// <summary>
    /// Un-register a new file to serve as the base for figuring out project roots
    /// </summary>
    void UnregisterFile(FullPath path);

    /// <summary>
    /// Return the last fully scanned <see cref="FileSystemSnapshot"/>.
    /// If scan has not finished yet, or if there are no registered files,
    /// the resulting snapshot is empty (but not <code>null</code>).
    /// </summary>
    FileSystemSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// Event raised when a file system scan operation has started
    /// </summary>
    event EventHandler<OperationInfo> SnapshotScanStarted;
    /// <summary>
    /// Event reaise when a file system scan operation has terminated
    /// successfully or due to an error (including cancellation).
    /// </summary>
    event EventHandler<SnapshotScanResult> SnapshotScanFinished;
    /// <summary>
    /// Event raised when files have changed on disk while the <see cref="CurrentSnapshot"/>
    /// remains unchanged.
    /// </summary>
    event EventHandler<FilesChangedEventArgs> FilesChanged;
  }
}
