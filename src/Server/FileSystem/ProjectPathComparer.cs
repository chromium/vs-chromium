// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Files;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public class ProjectPathComparer : IEqualityComparer<IProject> {
    public bool Equals(IProject x, IProject y) {
      return SystemPathComparer.Instance.StringComparer.Equals(x.RootPath, y.RootPath);
    }

    public int GetHashCode(IProject obj) {
      return SystemPathComparer.Instance.StringComparer.GetHashCode(obj.RootPath);
    }
  }
}
