// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  public interface IFileSystemNameFactory {
    /// <summary>
    /// Returns an absolute directory name instance from an absolute path.
    /// </summary>
    AbsoluteDirectoryName CreateAbsoluteDirectoryName(string path);

    /// <summary>
    /// Returns a <see cref="RelativeDirectoryName"/> instance from a parent
    /// directory and from a relative directory name.
    /// </summary>
    RelativeDirectoryName CreateDirectoryName(DirectoryName parent, RelativePathName relativeDirectoryName);

    /// <summary>
    /// Returns a <see cref="FileName"/> instance from a parent directory and a
    /// relative file name.
    /// </summary>
    FileName CreateFileName(DirectoryName parent, RelativePathName relativeFileName);

    /// <summary>
    /// Returns a <see cref="FileName"/> instance from the concatenation of a
    /// parent directory and a simple file name.
    /// </summary>
    FileName CombineFileName(DirectoryName parent, string fileName);

    /// <summary>
    /// Return a <see cref="RelativeDirectoryName"/> directory name from the
    /// concatenation of parent directory and a simple directory name.
    /// </summary>
    RelativeDirectoryName CombineDirectoryNames(DirectoryName parent, string directoryName);
  }
}
