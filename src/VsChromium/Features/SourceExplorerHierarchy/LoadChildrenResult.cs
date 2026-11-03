// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Protocol.Generated;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public struct LoadChildrenResult {
    public LoadChildrenResult(DirectoryNodeViewModel node, DirectoryEntries entry) {
      Node = node;
      Entry = entry;
    }

    public DirectoryNodeViewModel Node { get; }
    /// <summary>
    /// The entries of the directory, <code>null</code> if the directory is not
    /// known to the server.
    /// </summary>
    public DirectoryEntries Entry { get; }
  }
}
