// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.Projects {
  [Export(typeof(IRawProjectDiscovery))]
  public class RawProjectDiscovery : IRawProjectDiscovery {
    private readonly IProjectDiscoveryProvider[] _providers;

    [ImportingConstructor]
    public RawProjectDiscovery([ImportMany] IEnumerable<IProjectDiscoveryProvider> providers) {
      _providers = providers.OrderByDescending(x => x.Priority).ToArray();
    }

    public IProject GetProject(FullPathName filename) {
      return _providers
        .Select(t => t.GetProject(filename))
        .Where(project => project != null)
        .OrderByDescending(p => p.RootPath.Length)
        .FirstOrDefault();
    }

    public IProject GetProjectFromRootPath(FullPathName projectRootPath) {
      return _providers
        .Select(t => t.GetProjectFromRootPath(projectRootPath))
        .FirstOrDefault(project => project != null);
    }

    public void ValidateCache() {
      foreach (var provider in _providers) {
        provider.ValidateCache();
      }
    }
  }
}
