// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Wpf {
  /// <summary>
  /// Abstraction over a tree of Visual objects, where nodes have one parent and
  /// many children.
  /// </summary>
  public interface IHierarchyObject {
    bool IsVisual { get; }
    IHierarchyObject GetParent();
    IList<IHierarchyObject> GetAllChildren();
  }
}
