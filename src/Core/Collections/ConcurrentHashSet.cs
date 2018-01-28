// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;

namespace VsChromium.Core.Collections {
  public class ConcurrentHashSet<T> {
    private readonly SlimHashTable<T, T> _table;
    private readonly object _lock = new object();

    public ConcurrentHashSet() : this(0, 0.9, EqualityComparer<T>.Default) {
    }

    public ConcurrentHashSet(int capacity, double loadFactor) : this(capacity, loadFactor, EqualityComparer<T>.Default) {
    }

    public ConcurrentHashSet(int capacity, double loadFactor, IEqualityComparer<T> comparer) {
      _table = new SlimHashTable<T, T>(new Parameters(), capacity, loadFactor, comparer);
  }

    public T GetOrAdd(T value) {
      lock (_lock) {
        return _table.GetOrAdd(value, value);
      }
    }

    private class Parameters : ISlimHashTableParameters<T, T> {
      private readonly object _lock = new object();

      public Func<T, T> KeyGetter {
        get { return t => t; }
      }

      public Action Locker {
        get { return () => Monitor.Enter(_lock); }
      }

      public Action Unlnlocker {
        get { return () => Monitor.Exit(_lock); }
      }
    }
  }
}
