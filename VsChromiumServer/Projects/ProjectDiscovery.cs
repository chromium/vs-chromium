// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace VsChromiumServer.Projects {
  [Export(typeof(IProjectDiscovery))]
  public class ProjectDiscovery : IProjectDiscovery {
    private readonly IProjectDiscoveryProvider[] _providers;

    [ImportingConstructor]
    public ProjectDiscovery([ImportMany] IEnumerable<IProjectDiscoveryProvider> providers) {
      _providers = providers.OrderByDescending(x => x.Priority).ToArray();
    }

    public IProject GetProject(string filename) {
      for (var i = 0; i < _providers.Length; i++) {
        var project = _providers[i].GetProject(filename);
        if (project != null)
          return project;
      }
      return null;
    }

    public IProject GetProjectFromRootPath(string projectRootPath) {
      for (var i = 0; i < _providers.Length; i++) {
        var project = _providers[i].GetProjectFromRootPath(projectRootPath);
        if (project != null)
          return project;
      }
      return null;
    }

    public void ValidateCache() {
      for (var i = 0; i < _providers.Length; i++) {
        _providers[i].ValidateCache();
      }
    }
  }
}
