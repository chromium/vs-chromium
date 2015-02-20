// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Drawing;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class NodeViewModelTemplate {
    public NodeViewModelTemplate() {
      OpenFolderImageIndex = NodeViewModel.NoImage;
      ImageIndex = NodeViewModel.NoImage;
    }
    public int ImageIndex { get; set; }
    public int OpenFolderImageIndex { get; set; }
    public Icon Icon { get; set; }
    public Icon OpenFolderIcon { get; set; }
    public bool ExpandByDefault { get; set; }

    public static NodeViewModelTemplate Default = new NodeViewModelTemplate();
  }
}