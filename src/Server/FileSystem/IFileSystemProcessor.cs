// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Operations;

namespace VsChromium.Server.FileSystem {
  public interface IFileSystemProcessor {
    void Refresh();
    void RegisterFile(FullPath path);
    void UnregisterFile(FullPath path);
    FileSystemTreeSnapshot GetCurrentSnapshot();

    event EventHandler<OperationInfo> SnapshotScanStarted;
    event EventHandler<SnapshotScanResult> SnapshotScanFinished;
    event EventHandler<FilesChangedEventArgs> FilesChanged;
  }
}
