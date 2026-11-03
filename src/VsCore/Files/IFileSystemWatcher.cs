// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Core.Files {
  public interface IFileSystemWatcher : IDisposable {
    FullPath Path { get; }
    NotifyFilters NotifyFilter { get; set; }
    int InternalBufferSize { get; set; }
    bool IncludeSubdirectories { get; set;  }

    event FileSystemEventHandler Changed;
    event FileSystemEventHandler Created;
    event FileSystemEventHandler Deleted;
    event RenamedEventHandler Renamed;
    event ErrorEventHandler Error;

    void Start();
    void Stop();
  }
}