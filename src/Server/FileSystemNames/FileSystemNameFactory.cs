// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.FileSystemNames {
  [Export(typeof(IFileSystemNameFactory))]
  public class FileSystemNameFactory : IFileSystemNameFactory {

    public AbsoluteDirectoryName CreateAbsoluteDirectoryName(FullPathName path) {
      return new AbsoluteDirectoryName(path);
    }

    public RelativeDirectoryName CreateDirectoryName(DirectoryName parent, RelativePathName relativeDirectoryName) {
      return new RelativeDirectoryName(parent, relativeDirectoryName);
    }

    public FileName CreateFileName(DirectoryName parent, RelativePathName relativeFileName) {
      return new FileName(parent, relativeFileName);
    }

    public FileName CombineFileName(DirectoryName parent, string fileName) {
      var relativePath = parent.RelativePathName.CreateChild(fileName);
      return CreateFileName(parent, relativePath);
    }

    public RelativeDirectoryName CombineDirectoryNames(DirectoryName parent, string directoryName) {
      var relativePath = parent.RelativePathName.CreateChild(directoryName);
      return CreateDirectoryName(parent, relativePath);
    }

  }
}
