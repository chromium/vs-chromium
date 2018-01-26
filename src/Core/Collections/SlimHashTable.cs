// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Collections {
  public class SlimHashTable<TKey, TValue> : IEnumerable<TValue> {
    private static readonly double DefaultLoadFactor = 2.0;
    private readonly Func<TValue, TKey> _getKey;
    private readonly Action _locker;
    private readonly Action _unlocker;
    private readonly double _loadFactor;
    private readonly IEqualityComparer<TKey> _comparer;
    private Entry[] _entries;
    private int _count;
    private int _mask;

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters)
      : this(parameters, 0, DefaultLoadFactor, EqualityComparer<TKey>.Default) {
    }

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters, int capacity)
      : this(parameters, capacity, DefaultLoadFactor, EqualityComparer<TKey>.Default) {
    }

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters, int capacity, double loadFactor)
      : this(parameters, capacity, loadFactor, EqualityComparer<TKey>.Default) {
    }

    public SlimHashTable(ISlimHashTableParameters<TKey, TValue> parameters, int capacity, double loadFactor, IEqualityComparer<TKey> comparer) {
      Invariants.CheckArgumentNotNull(parameters, nameof(parameters));
      Invariants.CheckArgument(loadFactor >= 0.1, "Load factor is too small", nameof(loadFactor));
      Invariants.CheckArgumentNotNull(comparer, nameof(comparer));

      _getKey = parameters.KeyGetter;
      _locker = parameters.Locker;
      _unlocker = parameters.Unlnlocker;
      _loadFactor = loadFactor;
      _comparer = comparer;
      _mask = GetMask(capacity, loadFactor);
      _entries = new Entry[_mask + 1];
    }

    private static int GetMask(int capacity, double loadFactor) {
      var targetCapacity = (int)(capacity / loadFactor);
      for (var mask = 32; mask < int.MaxValue; mask = mask * 2) {
        if (targetCapacity < mask) {
          return mask - 1;
        }
      }
      throw Invariants.Fail("Dictionary capacity is greater than int.MaxValue");
    }

    public int Count => _count;

    public TValue this[TKey key] {
      get { return Get(key); }
      set { GetOrAdd(key, value); }
    }

    public void Clear() {
      using (new Locker(this)) {
        _mask = 31;
        _entries = new Entry[_mask + 1];
      }
    }

    public void Add(TKey key, TValue value) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        for (var e = _entries[hashCode & _mask]; e != null; e = e.Next) {
          if (comparer.Equals(key, _getKey(e.Value))) {
            Invariants.CheckArgument(false, nameof(key), "Key already exists in dictionary");
          }
        }

        AddEntry(value, hashCode);
      }
    }

    public bool Contains(TKey key) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        for (var e = _entries[hashCode & _mask]; e != null; e = e.Next) {
          if (comparer.Equals(key, _getKey(e.Value))) {
            return true;
          }
        }
      }

      return false;
    }

    public TValue Get(TKey key) {
      TValue result;
      if (!TryGetValue(key, out result)) {
        return default(TValue);
      }
      return result;
    }

    public bool TryGetValue(TKey key, out TValue value) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        for (var e = _entries[hashCode & _mask]; e != null; e = e.Next) {
          if (comparer.Equals(key, _getKey(e.Value))) {
            value = e.Value;
            return true;
          }
        }
      }

      value =default(TValue);
      return false;
    }

    public TValue GetOrAdd(TKey key, TValue value) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        for (var e = _entries[hashCode & _mask]; e != null; e = e.Next) {
          if (comparer.Equals(key, _getKey(e.Value))) {
            return e.Value;
          }
        }

        return AddEntry(value, hashCode);
      }
    }

    public void CopyTo<TArray>(TArray[] array, Func<TKey, TValue, TArray> convert, int arrayIndex) {
      using (new Locker(this)) {
        var index = arrayIndex;
        foreach (var entry in this) {
          array[index] = convert(_getKey(entry), entry);
          index++;
        }
      }
    }

    public bool Remove(TKey key) {
      var comparer = _comparer;
      var hashCode = comparer.GetHashCode(key);

      using (new Locker(this)) {
        Entry previous = null;
        for (var e = _entries[hashCode & _mask]; e != null; e = e.Next) {
          if (comparer.Equals(key, _getKey(e.Value))) {
            // Remove from linked list
            var next = e.Next;
            if (previous == null) {
              _entries[hashCode & _mask] = next;
            } else {
              previous.Next = next;
            }
            return true;
          }

          previous = e;
        }
      }

      return false;
    }

    public IEnumerator<TValue> GetEnumerator() {
      foreach (var e in _entries) {
        for (var head = e; head != null; head = head.Next) {
          yield return head.Value;
        }
      }
    }

    private TValue AddEntry(TValue value, int hashCode) {
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
      for (var i = 0; i < oldEntries.Length; i++) {
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
      internal readonly TValue Value;
      internal Entry Next;

      internal Entry(TValue value, Entry next) {
        Value = value;
        Next = next;
      }

      public int HashCode => Value.GetHashCode();
    }

    private struct Locker : IDisposable {
      private readonly SlimHashTable<TKey, TValue> _table;

      public Locker(SlimHashTable<TKey, TValue> table) {
        _table = table;
        _table._locker();
      }

      public void Dispose() {
        _table._unlocker();
      }
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }
  }
}