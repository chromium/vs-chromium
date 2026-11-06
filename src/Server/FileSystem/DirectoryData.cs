// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// Abstraction over immutable data related to a directory entry.
  /// </summary>
  public struct DirectoryData {
    private readonly DirectoryName _directoryName;
    private readonly bool _isSymLink;
    private readonly UInt128 _fileId;

    public DirectoryData(DirectoryName directoryName, bool isSymLink) : this(directoryName, isSymLink, default) {
    }

    public DirectoryData(DirectoryName directoryName, bool isSymLink, UInt128 fileId) {
      _directoryName = directoryName;
      _isSymLink = isSymLink;
      _fileId = fileId;
    }

    public DirectoryName DirectoryName {
      get { return _directoryName; }
    }

    public bool IsSymLink {
      get { return _isSymLink; }
    }

    /// <summary>
    /// The 128-bit file id of the directory (from the enumeration of its parent, see <see
    /// cref="Core.Files.DirectoryEntry.FileId"/>), 0 if unknown: used to map the records of the
    /// change journal to directories.
    /// </summary>
    public UInt128 FileId {
      get { return _fileId; }
    }

    public override string ToString() {
      return DirectoryName.ToString();
    }
  }
}
