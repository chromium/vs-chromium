// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyCommandHandler {
    public CommandID CommandId { get; set; }

    public Func<NodeViewModel, bool> IsEnabled { get; set; }
    public Action<NodeViewModel> Execute { get; set; }
  }
}
