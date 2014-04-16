// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.ObjectModel;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public class FileSystemTreeInternal {
    private readonly DirectoryEntryInternal _root;

    public FileSystemTreeInternal(DirectoryEntryInternal root) {
      _root = root;
    }

    public DirectoryEntryInternal Root { get { return _root; } }

    public static FileSystemTreeInternal Empty(IFileSystemNameFactory fileSystemNameFactory) {
      var emptyRoot = new DirectoryEntryInternal(fileSystemNameFactory.Root, new ReadOnlyCollection<FileSystemEntryInternal>(new FileSystemEntryInternal[0]));
      return new FileSystemTreeInternal(emptyRoot);
    }
  }
}