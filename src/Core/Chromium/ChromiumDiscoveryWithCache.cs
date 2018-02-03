// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;

namespace VsChromium.Core.Chromium {
  public class ChromiumDiscoveryWithCache<T> : IChromiumDiscoveryWithCache<T> {
    private readonly IChromiumDiscovery _chromiumDiscovery;
    private readonly FullPathDictionary<T> _chromiumRootDirectories = new FullPathDictionary<T>();
    private readonly FullPathDictionary<object> _nonChromiumPaths = new FullPathDictionary<object>();
    private readonly object _lock = new object();

    public ChromiumDiscoveryWithCache(IConfigurationSectionProvider configurationSectionProvider, IFileSystem fileSystem) {
      _chromiumDiscovery = new ChromiumDiscovery(fileSystem, configurationSectionProvider);
    }

    public T GetEnlistmentRootFromRootpath(FullPath root, Func<FullPath, T> factory) {
      lock (_lock) {
        return _chromiumRootDirectories.Get(root);
      }
    }

    public T GetEnlistmentRootFromAnyPath(FullPath path, Func<FullPath, T> factory) {
      lock (_lock) {
        // Cache hit?
        foreach (var parent in path.EnumeratePaths()) {
          T value;
          if (_chromiumRootDirectories.TryGet(parent, out value))
            return value;
        }

        // Negative cache hit?
        if (_nonChromiumPaths.Contains(path)) {
          return default(T);
        }
      }

      // Nope: compute all the way...
      return GetChromiumRootFolderWorker(path, factory);
    }

    public void ValidateCache() {
      lock (_lock) {
        _nonChromiumPaths.Clear();
        _chromiumRootDirectories.Clear();
      }
    }

    private T GetChromiumRootFolderWorker(FullPath path, Func<FullPath, T> factory) {
      var root = _chromiumDiscovery.GetEnlistmentRootPath(path);
      if (root == null) {
        lock (_lock) {
          // No one in the parent chain is a Chromium directory.
          path.EnumeratePaths().ForAll(x => _nonChromiumPaths.Add(x, null));
        }
        return default(T);
      }

      var result = factory(root.Value);
      lock (_lock) {
        _chromiumRootDirectories.Add(root.Value, result);
      }
      return result;
    }
  }
}
