// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.ObjectModel;
using VsChromium.Core.Win32.Files;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemSnapshot {
  /// <summary>
  /// A directory snapshot contains the directory name, the list of
  /// sub-directories and the list of file names directly contained in the
  /// directory.
  /// </summary>
  public class DirectorySnapshot {
    private readonly DirectoryData _directoryData;
    private readonly ReadOnlyCollection<DirectorySnapshot> _childDirectories;
    private readonly ReadOnlyCollection<FileName> _childFiles;

    public DirectorySnapshot(DirectoryData directoryData, ReadOnlyCollection<DirectorySnapshot> childDirectories, ReadOnlyCollection<FileName> childFiles) {
      _directoryData = directoryData;
      _childDirectories = childDirectories;
      _childFiles = childFiles;
    }

    public DirectoryData DirectoryData { get { return _directoryData; } }

    /// <summary>
    /// The directory name
    /// </summary>
    public DirectoryName DirectoryName { get { return DirectoryData.DirectoryName; } }
    /// <summary>
    /// The list of sub-directories.
    /// </summary>
    public ReadOnlyCollection<DirectorySnapshot> ChildDirectories { get { return _childDirectories; } }
    /// <summary>
    /// The list of files contained in this directory.
    /// </summary>
    public ReadOnlyCollection<FileName> ChildFiles { get { return _childFiles; } }
  }
}