// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Linq;
using VsChromiumCore.Configuration;
using VsChromiumCore.FileNames;
using VsChromiumCore.Linq;

namespace VsChromiumCore.Chromium {
  public class ChromiumDiscoveryWithCache<T> : IChromiumDiscoveryWithCache<T> {
    private readonly IChromiumDiscovery _chromiumDiscovery;
    private readonly FullPathNameSet<T> _chromiumRootDirectories = new FullPathNameSet<T>();
    private readonly FullPathNameSet<object> _nonChromiumDirectories = new FullPathNameSet<object>();
    private readonly object _lock = new object();

    public ChromiumDiscoveryWithCache(IConfigurationSectionProvider configurationSectionProvider) {
      this._chromiumDiscovery = new ChromiumDiscovery(configurationSectionProvider);
    }

    public T GetEnlistmentRootFromRootpath(FullPathName root, Func<FullPathName, T> factory) {
      lock (this._lock) {
        return this._chromiumRootDirectories.Get(root);
      }
    }

    public T GetEnlistmentRootFromFilename(FullPathName filename, Func<FullPathName, T> factory) {
      lock (this._lock) {
        // Cache hit?
        foreach (var parent in filename.EnumerateParents()) {
          T value;
          if (this._chromiumRootDirectories.TryGet(parent, out value))
            return value;
        }

        // Negative cache hit?
        if (this._nonChromiumDirectories.Contains(filename.Parent)) {
          return default(T);
        }
      }

      // Nope: compute all the way...
      return GetChromeRootFolderWorker(filename, factory);
    }

    public void ValidateCache() {
      lock (this._lock) {
        this._nonChromiumDirectories.RemoveWhere(x => !x.DirectoryExists);
        this._chromiumRootDirectories.RemoveWhere(x => !x.DirectoryExists);
      }
    }

    private T GetChromeRootFolderWorker(FullPathName filename, Func<FullPathName, T> factory) {
      var root = this._chromiumDiscovery.GetEnlistmentRoot(filename);
      if (root == default(FullPathName)) {
        lock (_lock) {
          // No one in the parent chain is a Chromium directory.
          filename.EnumerateParents().ForAll(x => this._nonChromiumDirectories.Add(x, null));
        }
        return default(T);
      }

      var result = factory(root);
      lock (_lock) {
        this._chromiumRootDirectories.Add(root, result);
      }
      return result;
    }
  }
}