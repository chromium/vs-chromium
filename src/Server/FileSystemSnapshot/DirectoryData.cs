// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemSnapshot {
  /// <summary>
  /// Abstraction over immutable data related to a directory entry.
  /// </summary>
  public struct DirectoryData {
    private readonly DirectoryName _directoryName;
    private readonly bool _isSymLink;

    public DirectoryData(DirectoryName directoryName, bool isSymLink) {
      _directoryName = directoryName;
      _isSymLink = isSymLink;
    }

    public DirectoryName DirectoryName {
      get { return _directoryName; }
    }

    public bool IsSymLink {
      get { return _isSymLink; }
    }
  }
}