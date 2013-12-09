// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystemNames;

namespace VsChromiumServer.FileSystem {
  public interface IFileSystemProcessor {
    void AddFile(string filename);
    void RemoveFile(string filename);
    FileSystemTree GetTree();

    event Action<long> TreeComputing;
    event Action<long, FileSystemTree, FileSystemTree> TreeComputed;
    event Action<IEnumerable<FileName>> FilesChanged;
  }
}
