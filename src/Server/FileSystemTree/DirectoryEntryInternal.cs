// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.ObjectModel;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public class DirectoryEntryInternal {
    private readonly DirectoryName _directoryName;
    private readonly ReadOnlyCollection<DirectoryEntryInternal> _directoryEntries;
    private readonly ReadOnlyCollection<FileName> _files;

    public DirectoryEntryInternal(DirectoryName directoryName, ReadOnlyCollection<DirectoryEntryInternal> directoryEntries, ReadOnlyCollection<FileName> files) {
      _directoryName = directoryName;
      _directoryEntries = directoryEntries;
      _files = files;
    }

    public DirectoryName DirectoryName { get { return _directoryName; } }
    public ReadOnlyCollection<DirectoryEntryInternal> DirectoryEntries { get { return _directoryEntries; } }
    public ReadOnlyCollection<FileName> Files { get { return _files; } }
  }
}