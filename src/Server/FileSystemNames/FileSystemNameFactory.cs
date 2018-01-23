// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;

namespace VsChromium.Server.FileSystemNames {
  [Export(typeof(IFileSystemNameFactory))]
  public class FileSystemNameFactory : IFileSystemNameFactory {
    private const int BucketCount = 31; // Prime number
    private readonly ConcurrentHashSet<string>[] _dictionaries;

    public FileSystemNameFactory() {
      _dictionaries = new ConcurrentHashSet<string>[BucketCount];
      for (var i = 0; i < _dictionaries.Length; i++) {
        _dictionaries[i] = new ConcurrentHashSet<string>(3.0);
      }
    }

    public DirectoryName CreateAbsoluteDirectoryName(FullPath path) {
      return new AbsoluteDirectoryName(path);
    }

    public FileName CreateFileName(DirectoryName parent, string name) {
      return new FileName(parent, InterName(name));
    }

    public DirectoryName CreateDirectoryName(DirectoryName parent, string name) {
      return new RelativeDirectoryName(parent, InterName(name));
    }

    private string InterName(string name) {
      return _dictionaries[name.Length % BucketCount].GetOrAdd(name);
    }
  }
}
