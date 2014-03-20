// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public interface IFileSystemNameFactory {
    /// <summary>
    /// Returns the root node of the file system name table. The Root node has no parent and an empty name.
    /// It can be used to create "real" directory and file names using the "CombineXxx" methods.
    /// </summary>
    DirectoryName Root { get; }

    /// <summary>
    /// Return a file name from the combination of a parent directory and a file name.
    /// </summary>
    FileName CombineFileName(DirectoryName parent, string fileName);

    /// <summary>
    /// Return a directory name from the combination of parent directory and a directory name.
    /// </summary>
    DirectoryName CombineDirectoryNames(DirectoryName parent, string directoryName);

    /// <summary>
    /// Return a directory name from the combination of the parent directory |parent| and the file name |relativeFileName|.
    /// </summary>
    FileName CreateFileName(DirectoryName parent, RelativePathName relativeFileName);

    /// <summary>
    /// Return a directory name from the combination of the parent directory |parent| and the directory name |relativeDirectoryName|.
    /// </summary>
    DirectoryName CreateDirectoryName(DirectoryName parent, RelativePathName relativeDirectoryName);
  }
}
