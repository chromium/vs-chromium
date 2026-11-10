// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Core.Files {
  public class FileSystemWatcherImpl : IFileSystemWatcher {
    private readonly FullPath _path;
    private FileSystemWatcher _fileSystemWatcherImplementation;
    private NotifyFilters _notifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
    private int _internalBufferSize = 8192;
    private bool _includeSubdirectories;

    public FileSystemWatcherImpl(FullPath path) {
      _path = path;
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
      Stop();
    }

    public FullPath Path {
      get { return _path; }
    }

    public NotifyFilters NotifyFilter {
      set {
        _notifyFilter = value;
        if (_fileSystemWatcherImplementation != null) {
          _fileSystemWatcherImplementation.NotifyFilter = value;
        }
      }
    }

    public int InternalBufferSize {
      set {
        _internalBufferSize = value;
        if (_fileSystemWatcherImplementation != null) {
          _fileSystemWatcherImplementation.InternalBufferSize = value;
        }
      }
    }

    public bool IncludeSubdirectories {
      set {
        _includeSubdirectories = value;
        if (_fileSystemWatcherImplementation != null) {
          _fileSystemWatcherImplementation.IncludeSubdirectories = value;
        }
      }
    }

    public event FileSystemEventHandler Changed;
    public event FileSystemEventHandler Created;
    public event FileSystemEventHandler Deleted;
    public event RenamedEventHandler Renamed;
    public event ErrorEventHandler Error;

    public void Start() {
      if (_fileSystemWatcherImplementation != null && _fileSystemWatcherImplementation.EnableRaisingEvents) {
        return;
      }

      Stop();

      FileSystemWatcher newImpl = null;
      try {
        newImpl = new FileSystemWatcher(Path.Value) {
          NotifyFilter = _notifyFilter,
          InternalBufferSize = _internalBufferSize,
          IncludeSubdirectories = _includeSubdirectories,
        };
        AddHandlers(newImpl);
        // Set _fileSystemWatcherImplementation before EnableRaisingEvents = true because on Linux
        // EnableRaisingEvents walks subdirectories synchronously and may fire Error re-entrantly.
        _fileSystemWatcherImplementation = newImpl;
        newImpl.EnableRaisingEvents = true;
      }
      catch (Exception e) {
        Stop();
        OnError(new ErrorEventArgs(e));
      }
    }

    public void Stop() {
      var impl = _fileSystemWatcherImplementation;
      if (impl == null) {
        return;
      }
      _fileSystemWatcherImplementation = null;
      RemoveHandlers(impl);
      try {
        if (impl.EnableRaisingEvents) {
          impl.EnableRaisingEvents = false;
        }
      }
      catch (Exception) {
        // Ignore errors when stopping a failed or disposed watcher.
      }
      try {
        impl.Dispose();
      }
      catch (Exception) {
      }
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
