// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    private class DirectoryWatcherhEntry {
      public FullPath Path { get; set; }
      public IFileSystemWatcher DirectoryNameWatcher { get; set; }
      public IFileSystemWatcher FileNameWatcher { get; set; }
      public IFileSystemWatcher FileWriteWatcher { get; set; }

      public void Dispose() {
        DirectoryNameWatcher?.Dispose();
        FileNameWatcher?.Dispose();
        FileWriteWatcher.Dispose();
      }

      public void Start() {
        DirectoryNameWatcher?.Start();
        FileNameWatcher?.Start();
        FileWriteWatcher?.Start();
      }

      public void Stop() {
        DirectoryNameWatcher?.Stop();
        FileNameWatcher?.Stop();
        FileWriteWatcher?.Stop();
      }
    }
  }
}