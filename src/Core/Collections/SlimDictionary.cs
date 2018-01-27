// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  public partial class SlimDictionary<TKey, TValue> : IDictionary<TKey, TValue> {
    private static readonly double DefaultLoadFactor = 0.9;
    private readonly object _syncRoot = new object();
    private readonly SlimHashTable<TKey, Entry> _table;
    private ValueCollection _values;

    public SlimDictionary() : this(0, EqualityComparer<TKey>.Default) {
    }

    public SlimDictionary(int capacity) : this(capacity, EqualityComparer<TKey>.Default) {
    }

    public SlimDictionary(int capacity, IEqualityComparer<TKey> comparer) {
      _table = new SlimHashTable<TKey, Entry>(new Parameters(), capacity, DefaultLoadFactor, comparer);
    }

    public ICollection<TKey> Keys => _table.Keys;

    public ICollection<TValue> Values {
      get {
        if (_values == null) {
          _values = new ValueCollection(this);
        }
        return _values;
      }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      return _table.Select(x => new KeyValuePair<TKey, TValue>(x.Key, x.Value.Value)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    public void Add(KeyValuePair<TKey, TValue> item) {
      _table.Add(item.Key, new Entry(item.Key, item.Value));
    }

    public void Clear() {
      _table.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return _table.Contains(item.Key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      _table.CopyTo(array, (k, v) => new KeyValuePair<TKey, TValue>(k, v.Value), arrayIndex);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
      return _table.Remove(item.Key);
    }

    public int Count {
      get { return _table.Count; }
    }

    public bool IsReadOnly {
      get { return false; }
    }

    public object SyncRoot {
      get { return _syncRoot; }
    }

    public bool ContainsKey(TKey key) {
      return _table.Contains(key);
    }

    public bool ContainsValue(TValue value) {
      if (ReferenceEquals(value, null)) {
        foreach (var entry in this) {
          if (entry.Value == null)
            return true;
        }
      } else {
        var equalityComparer = EqualityComparer<TValue>.Default;
        foreach (var entry in this) {
          if (equalityComparer.Equals(entry.Value, value)) {
            return true;
          }
        }
      }

      return false;
    }

    public void Add(TKey key, TValue value) {
      _table.Add(key, new Entry(key, value));
    }

    public bool Remove(TKey key) {
      return _table.Remove(key);
    }

    public bool TryGetValue(TKey key, out TValue value) {
      Entry entry;
      if (_table.TryGetValue(key, out entry)) {
        value = entry.Value;
        return true;
      }

      value = default(TValue);
      return false;
    }

    public TValue this[TKey key] {
      get { return _table[key].Value; }
      set { _table[key] = new Entry(key, value); }
    }
  }
}