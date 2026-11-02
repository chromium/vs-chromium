// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Core.Files {
  public class FileSystemWatcherImpl : IFileSystemWatcher {
    private readonly FullPath _path;
    private FileSystemWatcher _fileSystemWatcherImplementation;

    public FileSystemWatcherImpl(FullPath path) {
      _path = path;
      _fileSystemWatcherImplementation = new FileSystemWatcher(path.Value);
      AddHandlers(_fileSystemWatcherImplementation);
    }

    private void AddHandlers(FileSystemWatcher watcher) {
      watcher.Changed += FileSystemWatcherImplementationOnChanged;
      watcher.Created += FileSystemWatcherImplementationOnCreated;
      watcher.Deleted += FileSystemWatcherImplementationOnDeleted;
      watcher.Renamed += FileSystemWatcherImplementationOnRenamed;
      watcher.Error += FileSystemWatcherImplementationOnError;
    }

    private void RemoveHandlers(FileSystemWatcher watcher) {
      watcher.Changed -= FileSystemWatcherImplementationOnChanged;
      watcher.Created -= FileSystemWatcherImplementationOnCreated;
      watcher.Deleted -= FileSystemWatcherImplementationOnDeleted;
      watcher.Renamed -= FileSystemWatcherImplementationOnRenamed;
      watcher.Error -= FileSystemWatcherImplementationOnError;
    }

    private void FileSystemWatcherImplementationOnChanged(object o, FileSystemEventArgs args) {
      OnChanged(args);
    }

    private void FileSystemWatcherImplementationOnCreated(object o, FileSystemEventArgs args) {
      OnCreated(args);
    }

    private void FileSystemWatcherImplementationOnDeleted(object o, FileSystemEventArgs args) {
      OnDeleted(args);
    }

    private void FileSystemWatcherImplementationOnRenamed(object sender, RenamedEventArgs args) {
      OnRenamed(args);
    }

    private void FileSystemWatcherImplementationOnError(object o, ErrorEventArgs args) {
      OnError(args);
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
      if (_fileSystemWatcherImplementation.EnableRaisingEvents) {
        return;
      }

      var newImpl = new FileSystemWatcher(Path.Value);
      newImpl.NotifyFilter = _fileSystemWatcherImplementation.NotifyFilter;
      newImpl.InternalBufferSize = _fileSystemWatcherImplementation.InternalBufferSize;
      newImpl.IncludeSubdirectories = _fileSystemWatcherImplementation.IncludeSubdirectories;
      AddHandlers(newImpl);
      newImpl.EnableRaisingEvents = true;

      _fileSystemWatcherImplementation = newImpl;
    }

    public void Stop() {
      if (!_fileSystemWatcherImplementation.EnableRaisingEvents) {
        return;
      }
      _fileSystemWatcherImplementation.EnableRaisingEvents = false;
      RemoveHandlers(_fileSystemWatcherImplementation);
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