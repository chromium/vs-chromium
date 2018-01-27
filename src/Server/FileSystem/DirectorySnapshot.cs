// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// A directory snapshot contains the directory name, the list of
  /// sub-directories and the list of file names directly contained in the
  /// directory.
  /// </summary>
  public class DirectorySnapshot {
    private readonly DirectoryData _directoryData;
    private readonly IList<DirectorySnapshot> _childDirectories;
    private readonly IList<FileName> _childFiles;

    public DirectorySnapshot(DirectoryData directoryData, IList<DirectorySnapshot> childDirectories, IList<FileName> childFiles) {
      _directoryData = directoryData;
      _childDirectories = childDirectories;
      _childFiles = childFiles;
    }

    /// <summary>
    /// The directory name
    /// </summary>
    public DirectoryName DirectoryName {
      get { return _directoryData.DirectoryName; }
    }

    public bool IsSymLink {
      get { return _directoryData.IsSymLink; }
    }
    /// <summary>
    /// The list of sub-directories.
    /// </summary>
    public IList<DirectorySnapshot> ChildDirectories {
      get { return _childDirectories; }
    }

    /// <summary>
    /// The list of files contained in this directory.
    /// </summary>
    public IList<FileName> ChildFiles {
      get { return _childFiles; }
    }
  }
}