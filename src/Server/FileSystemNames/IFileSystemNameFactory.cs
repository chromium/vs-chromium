// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemNames {
  public interface IFileSystemNameFactory {
    /// <summary>
    /// Returns an absolute directory name instance from an absolute path.
    /// </summary>
    DirectoryName CreateAbsoluteDirectoryName(FullPath path);

    /// <summary>
    /// Returns a <see cref="RelativeDirectoryName"/> instance from a parent
    /// directory and from a simple directory name. <paramref name="isSymLink"/>
    /// is true if the directory is a symbolic link (see <see cref="DirectoryName.IsInSymLink"/>).
    /// </summary>
    DirectoryName CreateDirectoryName(DirectoryName parent, string name, bool isSymLink = false);

    /// <summary>
    /// Returns a <see cref="FileName"/> instance from the concatenation of a
    /// parent directory and a simple file name.
    /// </summary>
    FileName CreateFileName(DirectoryName parent, string name);

    void ClearInternedStrings();
  }
}
