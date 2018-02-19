// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public struct LoadChildrenResult {
    public LoadChildrenResult(DirectoryNodeViewModel node, DirectoryEntry entry) {
      Node = node;
      Entry = entry;
    }

    public DirectoryNodeViewModel Node { get; }
    public DirectoryEntry Entry { get; }
  }
}