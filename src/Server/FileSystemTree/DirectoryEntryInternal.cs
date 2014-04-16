// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.ObjectModel;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemTree {
  public class DirectoryEntryInternal : FileSystemEntryInternal {
    private readonly DirectoryName _directoryName;
    private readonly ReadOnlyCollection<FileSystemEntryInternal> _children;

    public DirectoryEntryInternal(DirectoryName directoryName, ReadOnlyCollection<FileSystemEntryInternal> children) {
      _directoryName = directoryName;
      _children = children;
    }

    public ReadOnlyCollection<FileSystemEntryInternal> Entries { get { return _children; } }

    public bool IsRoot { get { return FileSystemName.IsRoot; } }
    public DirectoryName DirectoryName { get { return _directoryName; } }
    public override FileSystemName FileSystemName { get { return _directoryName; } }
  }
}