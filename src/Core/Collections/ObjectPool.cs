// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Collections {
  public class ObjectPool<T> where T : class {
    private readonly Func<T> _creator;
    private readonly Action<T> _recycler;
    private readonly object _lock = new object();
    private readonly Entry[] _pool;
    private int _count;

    private struct Entry {
      internal T Value;
    }

    public ObjectPool(Func<T> creator, Action<T> recycler)
      : this(Environment.ProcessorCount, creator, recycler) {
    }

    public ObjectPool(int capacity, Func<T> creator, Action<T> recycler) {
      _count = 0;
      _pool = new Entry[capacity];
      _creator = creator;
      _recycler = recycler;
    }

    public FromPool<T> AcquireWithDisposable() {
      return new FromPool<T>(this, Acquire());
    }

    public T Acquire() {
      T result = null;
      lock (_lock) {
        if (_count > 0) {
          _count--;
          result = _pool[_count].Value;
          _pool[_count].Value = null;
        }
      }

      return result ?? _creator();
    }

    public void Release(T value) {
      _recycler(value);
      lock (_lock) {
        if (_count < _pool.Length) {
          _pool[_count].Value = value;
          _count++;
        }
      }
    }
  }

  public struct FromPool<T> : IDisposable where T : class {
    private readonly ObjectPool<T> _pool;
    private readonly T _value;

    public FromPool(ObjectPool<T> pool, T value) {
      _pool = pool;
      _value = value;
    }

    public T Value => _value;

    public void Dispose() {
      _pool?.Release(_value);
    }
  }
}