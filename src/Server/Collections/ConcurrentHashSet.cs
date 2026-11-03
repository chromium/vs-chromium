// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  public class ConcurrentHashSet<T> {
    private readonly SlimHashTable<T, T> _table;
    private readonly object _lock = new object();

    public ConcurrentHashSet(int capacity, ICollectionGrowthPolicy growthPolicy)
      : this(capacity, EqualityComparer<T>.Default, growthPolicy) {
    }

    public ConcurrentHashSet(int capacity, IEqualityComparer<T> comparer, ICollectionGrowthPolicy growthPolicy) {
      _table = new SlimHashTable<T, T>(t => t, capacity, comparer, growthPolicy);
    }

    public T GetOrAdd(T value) {
      lock (_lock) {
        return _table.GetOrAdd(value, value);
      }
    }

    public int Count {
      get {
        lock (_lock) {
          return _table.Count;
        }
      }
    }

    public void Clear() {
      lock (_lock) {
        _table.Clear();
      }
    }
  }
}
