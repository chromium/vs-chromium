// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.ObjectModel;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem.Snapshot {
  /// <summary>
  /// A directory snapshot contains the directory name, the list of
  /// sub-directories and the list of file names directly contained in the
  /// directory.
  /// </summary>
  public class DirectorySnapshot {
    private readonly DirectoryName _directoryName;
    private readonly ReadOnlyCollection<DirectorySnapshot> _directoryEntries;
    private readonly ReadOnlyCollection<FileName> _files;

    public DirectorySnapshot(DirectoryName directoryName, ReadOnlyCollection<DirectorySnapshot> directoryEntries, ReadOnlyCollection<FileName> files) {
      _directoryName = directoryName;
      _directoryEntries = directoryEntries;
      _files = files;
    }

    /// <summary>
    /// The directory name
    /// </summary>
    public DirectoryName DirectoryName { get { return _directoryName; } }
    /// <summary>
    /// The list of sub-directories.
    /// </summary>
    public ReadOnlyCollection<DirectorySnapshot> DirectoryEntries { get { return _directoryEntries; } }
    /// <summary>
    /// The list of files contained in this directory.
    /// </summary>
    public ReadOnlyCollection<FileName> Files { get { return _files; } }
  }
}