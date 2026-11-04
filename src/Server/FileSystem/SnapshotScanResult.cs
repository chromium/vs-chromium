// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.Operations;

namespace VsChromium.Server.FileSystem {
  public class SnapshotScanResult {
    public OperationInfo OperationInfo { get; set; }
    /// <summary>
    /// Error if the file loading operation did not complete successfully.
    /// <code>null</code> if successful completion.
    /// </summary>
    public Exception Error { get; set; }
    /// <summary>Property is <code>null</code> if <see cref="Error"/> is set.</summary>
    public FileSystemSnapshot PreviousSnapshot { get; set; }
    /// <summary>Property is <code>null</code> if <see cref="Error"/> is set.</summary>
    public FileSystemSnapshot NewSnapshot { get; set; }
    /// <summary>Maybe <code>null</code> if changes are not known.</summary>
    public FullPathChanges FullPathChanges { get; set; }
    /// <summary>
    /// True if the index of the new snapshot is the same as the index cache (verification of a
    /// cached tree without change): the cache is not saved.
    /// </summary>
    public bool CacheUnchanged { get; set; }
  }
}
