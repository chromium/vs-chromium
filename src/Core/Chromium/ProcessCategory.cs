// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Chromium {
  public enum ProcessCategory {
    Unknown,
    Browser,
    Renderer,
    Gpu,
    Plugin,
    Ppapi,
    PpapiBroker,
    DelegateExecute,
    MetroViewer,
    Service,
    Other
  }

  // Defines an extension method for the ProcessCategory enum which converts the enum value into
  // the group title.
  public static class ProcessCategoryExtensions {
    public static string ToGroupTitle(this ProcessCategory category) {
      switch (category) {
        case ProcessCategory.DelegateExecute:
          return "Delegate Execute";
        case ProcessCategory.MetroViewer:
          return "Metro Viewer";
        case ProcessCategory.PpapiBroker:
          return "Ppapi Broker";
        default:
          return category.ToString();
      }
    }
  }
}
