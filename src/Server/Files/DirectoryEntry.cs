// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;

namespace VsChromium.Core.Files {
  /// <summary>
  /// File or directory entry information returned by <see
  /// cref="IFileSystem.GetDirectoryEntries"/>, including the short <see
  /// cref="Name"/> and file attributes.
  /// </summary>
  public struct DirectoryEntry {
    private readonly string _name;
    private readonly FileAttributes _attributes;
    private readonly FileMetadata _metadata;

    public DirectoryEntry(string name, FileAttributes attributes) : this(name, attributes, default) {
    }

    public DirectoryEntry(string name, FileAttributes attributes, FileMetadata metadata) {
      _metadata = metadata;
      _name = name;
      _attributes = attributes;
    }

    public string Name {
      get { return _name; }
    }

    /// <summary>
    /// The last write time and size of the file (from the directory enumeration, no file opened).
    /// </summary>
    public FileMetadata Metadata {
      get { return _metadata; }
    }

    public bool IsFile {
      get {
        return (_attributes & FileAttributes.Directory) == 0;
      }
    }

    public bool IsDirectory {
      get {
        return (_attributes & FileAttributes.Directory) != 0;
      }
    }

    public bool IsSymLink {
      get {
        return (_attributes & FileAttributes.ReparsePoint) != 0;
      }
    }
  }
}
