// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public interface INodeViewModelLoader {
    /// <summary>
    /// Retrieves from the server the <see cref="DirectoryEntry"/> that corresponds 
    /// to the <see cref="DirectoryNodeViewModel"/> <paramref name="node"/>.
    /// Returns <code>null</code> if the directory is not known to the server.
    /// 
    /// <para>This method involves a round trip to the index server, but can be called
    /// from any thread.</para>
    /// </summary>
    DirectoryEntry LoadChildren(DirectoryNodeViewModel node);

    List<LoadChildrenEntry> LoadChildrenMultiple(RootNodeViewModel projectNode,
      ICollection<DirectoryNodeViewModel> nodes);
  }

  public struct LoadChildrenEntry {
    public LoadChildrenEntry(DirectoryNodeViewModel node, DirectoryEntry entry) {
      Node = node;
      Entry = entry;
    }

    public DirectoryNodeViewModel Node { get; }
    public DirectoryEntry Entry { get; }
  }
}