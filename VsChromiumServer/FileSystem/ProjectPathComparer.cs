// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromiumCore.FileNames;
using VsChromiumServer.Projects;

namespace VsChromiumServer.FileSystem {
  public class ProjectPathComparer : IEqualityComparer<IProject> {
    public bool Equals(IProject x, IProject y) {
      return SystemPathComparer.Instance.Comparer.Equals(x.RootPath, y.RootPath);
    }

    public int GetHashCode(IProject obj) {
      return SystemPathComparer.Instance.Comparer.GetHashCode(obj.RootPath);
    }
  }
}
