// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyChanges {
    public VsHierarchyChanges() {
      DeletedItems = new List<uint>();
      AddedItems = new List<uint>();
    }

    /// <summary>
    /// Items deleted in the previous version of the nodes
    /// </summary>
    public List<uint> DeletedItems { get; set; }
    /// <summary>
    /// Items added in the new version of the nodes
    /// </summary>
    public List<uint> AddedItems { get; set; }
  }
}
