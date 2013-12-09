// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.FileNames;

namespace VsChromiumServer.FileSystemNames {
  [Export(typeof(IFileSystemNameFactory))]
  public class FileSystemNameFactory : IFileSystemNameFactory {
    private readonly DirectoryName _root = new DirectoryName(null, "");

    /// <summary>
    /// Returns the root node of the file system name table. The Root node has no parent and an empty name.
    /// It can be used to create "real" directory and file names using the "CombineXxx" methods.
    /// </summary>
    public DirectoryName Root {
      get {
        return this._root;
      }
    }

    public FileName CombineFileName(DirectoryName parent, string fileName) {
      return new FileName(parent, fileName);
    }

    public DirectoryName CombineDirectoryNames(DirectoryName parent, string directoryName) {
      return new DirectoryName(parent, directoryName);
    }

    public FileName CreateFileName(DirectoryName parent, RelativePathName relativeFileName) {
      return new FileName(parent, relativeFileName);
    }

    public DirectoryName CreateDirectoryName(DirectoryName parent, RelativePathName relativeDirectoryName) {
      return new DirectoryName(parent, relativeDirectoryName);
    }
  }
}
