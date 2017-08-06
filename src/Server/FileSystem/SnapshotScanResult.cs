// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.FileSystemScanSnapshot;
using VsChromium.Server.Operations;

namespace VsChromium.Server.FileSystem {
  public class SnapshotScanResult {
    public OperationInfo OperationInfo { get; set; }
    public Exception Error { get; set; }
    public FileSystemSnapshot PreviousSnapshot { get; set; }
    public FileSystemSnapshot NewSnapshot { get; set; }
    /// <summary>Maybe <code>null</code> if changes are not known.</summary>
    public FullPathChanges FullPathChanges { get; set; }
  }
}