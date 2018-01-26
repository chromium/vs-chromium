// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;

namespace VsChromium.Core.Collections {
  public class ConcurrentHashSet<T> : SlimHashTable<T, T> {

    public ConcurrentHashSet() : this(0.8, EqualityComparer<T>.Default) {
    }

    public ConcurrentHashSet(double loadFactor) : this(loadFactor, EqualityComparer<T>.Default) {
    }

    public ConcurrentHashSet(double loadFactor, IEqualityComparer<T> comparer)
      : base(new Parameters(), 0, loadFactor, comparer) {
    }

    public T GetOrAdd(T value) {
      return GetOrAdd(value, value);
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
