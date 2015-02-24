// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Linq;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyAggregate : IVsHierarchyImpl {
    private readonly List<VsHierarchyCommandHandler> _commandHandlers = new List<VsHierarchyCommandHandler>();
    private readonly List<VsHierarchy> _hierarchies = new List<VsHierarchy>(); 
    private int _version;

    public VsHierarchyAggregate() {
      _version = 1;
    }

    public int Version { get { return _version; } }
    public bool IsEmpty { get { return _hierarchies.Count == 0; } }

    public void AddCommandHandler(VsHierarchyCommandHandler handler) {
      _commandHandlers.Add(handler);
    }

    public void Reconnect() {
      _hierarchies.ForAll(x => x.Reconnect());
    }

    public void Disconnect() {
      _hierarchies.ForAll(x => x.Disconnect());
    }

    public void Disable() {
      _hierarchies.ForAll(x => x.Disconnect());
    }

    public void SelectNodeByFilePath(string filePath) {
      _hierarchies.ForAll(x => x.SelectNodeByFilePath(filePath));
    }
  }
}