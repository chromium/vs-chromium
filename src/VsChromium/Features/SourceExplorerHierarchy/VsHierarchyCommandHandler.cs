// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Design;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyCommandHandler {
    public CommandID CommandId { get; set; }

    public Func<NodeViewModel, bool> IsEnabled { get; set; }
    public Action<CommandArgs> Execute { get; set; }
  }

  public class CommandArgs {
    public CommandArgs(CommandID commandId, VsHierarchy vsHierarchy, NodeViewModel node, IntPtr variantIn, IntPtr variantOut) {
      VsHierarchy = vsHierarchy;
      CommandId = commandId;
      Node = node;
      VariantIn = variantIn;
      VariantOut = variantOut;
    }

    public CommandID CommandId { get; private set; }
    public VsHierarchy VsHierarchy { get; private set; }
    public NodeViewModel Node { get; private set; }
    public IntPtr VariantIn { get; private set; }
    public IntPtr VariantOut { get; private set; }
  }
}
