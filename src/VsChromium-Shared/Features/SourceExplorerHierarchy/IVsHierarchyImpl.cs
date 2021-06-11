// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Features.SourceExplorerHierarchy {
  public interface IVsHierarchyImpl {
    int Version { get; }
    bool IsEmpty { get; }

    void AddCommandHandler(VsHierarchyCommandHandler handler);
    void Reconnect();
    void Disconnect();
    void Disable();
    void SelectNodeByFilePath(string filePath);
  }
}