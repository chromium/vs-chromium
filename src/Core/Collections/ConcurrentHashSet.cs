// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  public class ConcurrentHashSet<T> {
    private readonly SlimHashTable<T, T> _table;
    private readonly object _lock = new object();

    public ConcurrentHashSet() : this(0, EqualityComparer<T>.Default) {
    }

    public ConcurrentHashSet(int capacity) : this(capacity, EqualityComparer<T>.Default) {
    }

    public ConcurrentHashSet(int capacity, IEqualityComparer<T> comparer) {
      _table = new SlimHashTable<T, T>(t => t, capacity, comparer);
    }

    public T GetOrAdd(T value) {
      lock (_lock) {
        return _table.GetOrAdd(value, value);
      }
    }

    public void Clear() {
      lock (_lock) {
        _table.Clear();
      }
    }
  }
}
