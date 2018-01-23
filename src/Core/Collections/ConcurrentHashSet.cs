// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  public class ConcurrentHashSet<T> {
    private readonly double _loadFactor;
    private readonly IEqualityComparer<T> _comparer;
    private Entry[] _entries;
    private int _count;
    private int _mask;

    public ConcurrentHashSet() : this(0.8, EqualityComparer<T>.Default) {
    }

    public ConcurrentHashSet(double loadFactor) : this(loadFactor, EqualityComparer<T>.Default) {
    }

    public ConcurrentHashSet(double loadFactor, IEqualityComparer<T> comparer) {
      _loadFactor = loadFactor;
      _comparer = comparer;
      _mask = 31;
      _entries = new Entry[_mask + 1];
    }

    public T GetOrAdd(T key) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      lock (this) {
        for (var e = _entries[hashCode & _mask]; e != null; e = e.Next) {
          if (comparer.Equals(key, e.Value)) {
            return e.Value;
          }
        }
        return AddEntry(key, hashCode);
      }
    }

    public T Get(T value) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(value);

      lock (this) {
        for (var e = _entries[hashCode & _mask]; e != null; e = e.Next) {
          if (comparer.Equals(value, e.Value)) {
            return e.Value;
          }
        }
      }

      return default(T);
    }

    private T AddEntry(T value, int hashCode) {
      var index = hashCode & _mask;
      var e = new Entry(value, _entries[index]);
      _entries[index] = e;
      _count++;
      if ((float)_count / _entries.Length >= _loadFactor) {
        Grow();
      }
      return e.Value;
    }

    private void Grow() {
      int newMask = _mask * 2 + 1;
      var oldEntries = _entries;
      var newEntries = new Entry[newMask + 1];

      // use oldEntries.Length to eliminate the rangecheck            
      for (int i = 0; i < oldEntries.Length; i++) {
        var e = oldEntries[i];
        while (e != null) {
          var newIndex = e.HashCode & newMask;
          var tmp = e.Next;
          e.Next = newEntries[newIndex];
          newEntries[newIndex] = e;
          e = tmp;
        }
      }

      _entries = newEntries;
      _mask = newMask;
    }

    private class Entry {
      internal readonly T Value;
      internal Entry Next;

      internal Entry(T value, Entry next) {
        Value = value;
        Next = next;
      }

      public int HashCode => Value.GetHashCode();
    }
  }
}
