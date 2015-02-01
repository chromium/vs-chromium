// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Win32.Files;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemSnapshot {
  /// <summary>
  /// Abstraction over immutable data related to a directory entry.
  /// </summary>
  public struct DirectoryData {
    private readonly DirectoryName _directoryName;
    private readonly DirectoryEntry _directoryEntry;

    public DirectoryData(DirectoryName directoryName, DirectoryEntry directoryEntry) {
      _directoryEntry = directoryEntry;
      _directoryName = directoryName;
    }

    public DirectoryName DirectoryName {
      get { return _directoryName; }
    }

    public DirectoryEntry DirectoryEntry {
      get { return _directoryEntry; }
    }
  }
}