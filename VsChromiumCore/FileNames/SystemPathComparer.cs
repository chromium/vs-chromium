// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumCore.FileNames {
  public static class SystemPathComparer {
    public static IPathComparer Instance {
      get {
        // TODO(rpaquay): Maybe one day we will support *nix file systems.
        return CaseInsensitivePathComparer.Instance;
      }
    }
  }
}
