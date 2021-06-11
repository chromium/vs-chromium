// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class IncrementalBuildResult {
    public VsHierarchyNodes OldNodes { get; set; }
    public VsHierarchyNodes NewNodes { get; set; }
    public VsHierarchyChanges Changes { get; set; }
    public Dictionary<string, NodeViewModelTemplate> FileTemplatesToInitialize { get; set; }
  }
}