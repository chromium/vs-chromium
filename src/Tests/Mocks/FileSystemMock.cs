// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Tests.Mocks {
  class FileSystemMock : IFileSystem {
    private readonly DirectoryMock _rootDirectory = new DirectoryMock(null, null);

    public class EntryMock {
      private readonly DirectoryMock _parent;
      private readonly string _name;

      public EntryMock(DirectoryMock parent, string name) {
        if (parent == null && name != null) {
          throw new ArgumentException("Root directory must not have a name.");
        }
        if (parent != null) {
          if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Entry name is empty.");
          if (name.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            throw new ArgumentException("Entry name contains alternate path separators.");
        }

        _parent = parent;
        _name = name;
      }

      public string Name {
        get { return _name; }
      }

      public DirectoryMock Parent {
        get { return _parent; }
      }
    }

    public class DirectoryMock : EntryMock {
      private readonly List<DirectoryMock> _directories = new List<DirectoryMock>();
      private readonly List<FileMock> _files = new List<FileMock>();

      public DirectoryMock(DirectoryMock parent, string name)
        : base(parent, name) {
      }

      public List<DirectoryMock> Directories {
        get { return _directories; }
      }

      public List<FileMock> Files {
        get { return _files; }
      }

      public DirectoryMock AddDirectory(string name) {
        // Only the root directory can contain directory separators.
        if (!IsRoot) {
          if (name.IndexOf(Path.DirectorySeparatorChar) >= 0) {
            throw new ArgumentException();
          }
        }
        var dir = new DirectoryMock(this, name);
        _directories.Add(dir);
        return dir;
      }

      public bool IsRoot {
        get { return Parent == null; }
      }

      public FileMock AddFile(string name, string text) {
        var file = new FileMock(this, name, text);
        _files.Add(file);
        return file;
      }
    }

    public class FileMock : EntryMock {
      public FileMock(DirectoryMock parent, string name, string text)
        : base(parent, name) {
        Text = text;
      }

      public string Text { get; set; }

    }

    public class FileInfoSnapshotMock : IFileInfoSnapshot {
      public FullPath Path { get; set; }
      public bool Exists { get; set; }
      public bool IsFile { get; set; }
      public bool IsDirectory { get; set; }
      public bool IsSymLink { get; set; }
      public DateTime LastWriteTimeUtc { get; set; }
      public long Length { get; set; }
    }

    private EntryMock FindEntry(FullPath path) {
      var current = _rootDirectory
        .Directories
        .FirstOrDefault(x => PathHelpers.IsPrefix(path.Value, x.Name));
      if (current == null)
        return null;
      var splitPath = PathHelpers.SplitPrefix(path.Value, current.Name);
      var names = splitPath.Suffix.Split(Path.DirectorySeparatorChar);
      foreach (var name in names) {
        // File name can only be the last part of the path
        if (name == names.Last()) {
          var file = current.Files.FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, name));
          if (file != null)
            return file;
        }
        current = current.Directories.FirstOrDefault(x => SystemPathComparer.Instance.StringComparer.Equals(x.Name, name));
        if (current == null)
          return null;
      }
      return current;
    }

    public IFileInfoSnapshot GetFileInfoSnapshot(FullPath path) {
      var entry = FindEntry(path);
      if (entry == null) {
        return new FileInfoSnapshotMock {
          Path = path,
          Exists = false,
        };
      }

      if (entry is DirectoryMock) {
        return new FileInfoSnapshotMock {
          Path = path,
          Exists = true,
          IsDirectory = true,
        };
      }

      if (entry is FileMock) {
        return new FileInfoSnapshotMock {
          Path = path,
          Exists = true,
          IsFile = true,
        };
      }

      throw new ArgumentException();
    }

    public IList<string> ReadAllLines(FullPath path) {
      var entry = FindEntry(path) as FileMock;
      if (entry == null)
        throw new ArgumentException();

      using (var stream = new StringReader(entry.Text)) {
        var result = new List<string>();
        while (true) {
          var line = stream.ReadLine();
          if (line == null)
            break;
          result.Add(line);
        }
        return result;
      }
    }

    public SafeHeapBlockHandle ReadFileNulTerminated(FullPath path, long fileSize, int trailingByteCount) {
      throw new NotImplementedException();
    }

    public IList<DirectoryEntry> GetDirectoryEntries(FullPath path) {
      throw new NotImplementedException();
    }

    public IFileSystemWatcher CreateDirectoryWatcher(FullPath path) {
      throw new NotImplementedException();
    }

    public DirectoryMock AddDirectory(string name) {
      return _rootDirectory.AddDirectory(name);
    }
  }
}
