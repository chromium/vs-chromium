// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Configuration;
using VsChromium.Core.FileNames;
using VsChromium.Core.Linq;

namespace VsChromium.Core.Chromium {
  public class ChromiumDiscoveryWithCache<T> : IChromiumDiscoveryWithCache<T> {
    private readonly IChromiumDiscovery _chromiumDiscovery;
    private readonly FullPathNameSet<T> _chromiumRootDirectories = new FullPathNameSet<T>();
    private readonly FullPathNameSet<object> _nonChromiumDirectories = new FullPathNameSet<object>();
    private readonly object _lock = new object();

    public ChromiumDiscoveryWithCache(IConfigurationSectionProvider configurationSectionProvider) {
      _chromiumDiscovery = new ChromiumDiscovery(configurationSectionProvider);
    }

    public T GetEnlistmentRootFromRootpath(FullPathName root, Func<FullPathName, T> factory) {
      lock (_lock) {
        return _chromiumRootDirectories.Get(root);
      }
    }

    public T GetEnlistmentRootFromFilename(FullPathName filename, Func<FullPathName, T> factory) {
      lock (_lock) {
        // Cache hit?
        foreach (var parent in filename.EnumerateParents()) {
          T value;
          if (_chromiumRootDirectories.TryGet(parent, out value))
            return value;
        }

        // Negative cache hit?
        if (_nonChromiumDirectories.Contains(filename.Parent)) {
          return default(T);
        }
      }

      // Nope: compute all the way...
      return GetChromeRootFolderWorker(filename, factory);
    }

    public void ValidateCache() {
      lock (_lock) {
        _nonChromiumDirectories.RemoveWhere(x => !x.Key.DirectoryExists);
        _chromiumRootDirectories.RemoveWhere(x => !x.Key.DirectoryExists);
      }
    }

    private T GetChromeRootFolderWorker(FullPathName filename, Func<FullPathName, T> factory) {
      var root = _chromiumDiscovery.GetEnlistmentRoot(filename);
      if (root == default(FullPathName)) {
        lock (_lock) {
          // No one in the parent chain is a Chromium directory.
          filename.EnumerateParents().ForAll(x => _nonChromiumDirectories.Add(x, null));
        }
        return default(T);
      }

      var result = factory(root);
      lock (_lock) {
        _chromiumRootDirectories.Add(root, result);
      }
      return result;
    }
  }
}
