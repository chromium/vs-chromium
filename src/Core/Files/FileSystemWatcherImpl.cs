// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;

namespace VsChromium.Core.Files {
  public class FileSystemWatcherImpl : IFileSystemWatcher {
    private readonly FullPath _path;
    private readonly FileSystemWatcher _fileSystemWatcherImplementation;

    public FileSystemWatcherImpl(FullPath path) {
      _path = path;
      _fileSystemWatcherImplementation = new FileSystemWatcher(_path.Value);
    }

    public void Dispose() {
      _fileSystemWatcherImplementation.Dispose();
    }

    public FullPath DirectoryPath {
      get { return _path; }
    }

    public NotifyFilters NotifyFilter {
      get { return _fileSystemWatcherImplementation.NotifyFilter; }
      set { _fileSystemWatcherImplementation.NotifyFilter = value; }
    }

    public int InternalBufferSize {
      get { return _fileSystemWatcherImplementation.InternalBufferSize; }
      set { _fileSystemWatcherImplementation.InternalBufferSize = value; }
    }

    public bool IncludeSubdirectories {
      get { return _fileSystemWatcherImplementation.IncludeSubdirectories; }
      set { _fileSystemWatcherImplementation.IncludeSubdirectories = value; }
    }

    public event FileSystemEventHandler Changed {
      add { _fileSystemWatcherImplementation.Changed += value; }
      remove { _fileSystemWatcherImplementation.Changed -= value; }
    }

    public event FileSystemEventHandler Created {
      add { _fileSystemWatcherImplementation.Created += value; }
      remove { _fileSystemWatcherImplementation.Created -= value; }
    }

    public event FileSystemEventHandler Deleted {
      add { _fileSystemWatcherImplementation.Deleted += value; }
      remove { _fileSystemWatcherImplementation.Deleted -= value; }
    }

    public event RenamedEventHandler Renamed {
      add { _fileSystemWatcherImplementation.Renamed += value; }
      remove { _fileSystemWatcherImplementation.Renamed -= value; }
    }

    public event ErrorEventHandler Error {
      add { _fileSystemWatcherImplementation.Error += value; }
      remove { _fileSystemWatcherImplementation.Error -= value; }
    }

    public void Start() {
      _fileSystemWatcherImplementation.EnableRaisingEvents = true;
    }

    public void Stop() {
      _fileSystemWatcherImplementation.EnableRaisingEvents = false;
    }
  }
}