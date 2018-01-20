// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;

namespace VsChromium.Core.Files {
  public class FileSystemWatcherImpl : IFileSystemWatcher {
    private readonly FullPath _path;
    private FileSystemWatcher _fileSystemWatcherImplementation;

    public FileSystemWatcherImpl(FullPath path) {
      _path = path;
      _fileSystemWatcherImplementation = new FileSystemWatcher(path.Value);
      _fileSystemWatcherImplementation.Changed += (sender, args) => OnChanged(args);
      _fileSystemWatcherImplementation.Created += (sender, args) => OnCreated(args);
      _fileSystemWatcherImplementation.Deleted += (sender, args) => OnDeleted(args);
      _fileSystemWatcherImplementation.Renamed += (sender, args) => OnRenamed(args);
      _fileSystemWatcherImplementation.Error += (sender, args) => OnError(args);
    }

    public void Dispose() {
      _fileSystemWatcherImplementation.Dispose();
    }

    public FullPath Path {
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

    public event FileSystemEventHandler Changed;
    public event FileSystemEventHandler Created;
    public event FileSystemEventHandler Deleted;
    public event RenamedEventHandler Renamed;
    public event ErrorEventHandler Error;

    public void Start() {
      _fileSystemWatcherImplementation.EnableRaisingEvents = true;
    }

    public void Stop() {
      _fileSystemWatcherImplementation.EnableRaisingEvents = false;
    }

    protected virtual void OnChanged(FileSystemEventArgs e) {
      Changed?.Invoke(this, e);
    }

    protected virtual void OnCreated(FileSystemEventArgs e) {
      Created?.Invoke(this, e);
    }

    protected virtual void OnDeleted(FileSystemEventArgs e) {
      Deleted?.Invoke(this, e);
    }

    protected virtual void OnRenamed(RenamedEventArgs e) {
      Renamed?.Invoke(this, e);
    }

    protected virtual void OnError(ErrorEventArgs e) {
      Error?.Invoke(this, e);
    }
  }
}