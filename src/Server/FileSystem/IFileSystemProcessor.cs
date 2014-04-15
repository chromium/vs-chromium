// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemTree;

namespace VsChromium.Server.FileSystem {
  public interface IFileSystemProcessor {
    void AddFile(string filename);
    void RemoveFile(string filename);
    VersionedFileSystemTreeInternal GetTree();

    event Action<long> TreeComputing;
    event Action<long, VersionedFileSystemTreeInternal, VersionedFileSystemTreeInternal> TreeComputed;
    event Action<IEnumerable<FileName>> FilesChanged;
  }
}
