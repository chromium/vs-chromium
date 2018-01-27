// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  public class ReadOnlyMapSortedList<TKey, TValue> : IReadOnlyMap<TKey, TValue> {
    private readonly IList<KeyValuePair<TKey, TValue>> _entries;
    private readonly IComparer<TKey> _comparer;
    private readonly Func<KeyValuePair<TKey, TValue>, TKey, int> _itemComparer;
    private readonly object _syncRoot = new object();
    private KeyCollection _keys;
    private ValueCollection _values;

    public ReadOnlyMapSortedList(KeyValuePair<TKey, TValue>[] entries) {
      _entries = entries;
      _comparer = Comparer<TKey>.Default;
      _itemComparer = ItemComparer;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      return _entries.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    public void Add(KeyValuePair<TKey, TValue> item) {
      throw new NotSupportedException();
    }

    public void Clear() {
      throw new NotSupportedException();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return FindIndex(item.Key) >= 0;
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      _entries.CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
      throw new NotSupportedException();
    }

    public int Count {
      get { return _entries.Count; }
    }

    public bool IsReadOnly {
      get { return true; }
    }

    public TValue this[TKey key] {
      get {
        TValue value;
        if (!TryGetValue(key, out value)) {
          throw new ArgumentException("Key not found", nameof(key));
        }
        return value;
      }
    }

    public object SyncRoot {
      get { return _syncRoot; }
    }

    public bool TryGetValue(TKey key, out TValue value) {
      var index = FindIndex(key);
      if (index < 0) {
        value = default(TValue);
        return false;
      }
      value = _entries[index].Value;
      return true;
    }

    public bool ContainsKey(TKey key) {
      return FindIndex(key) >= 0;
    }

    public ICollection<TKey> Keys {
      get {
        if (_keys == null) {
          _keys = new KeyCollection(this);
        }
        return _keys;
      }
    }

    public ICollection<TValue> Values {
      get {
        if (_values == null) {
          _values = new ValueCollection(this);
        }
        return _values;
      }
    }

    private int FindIndex(TKey key) {
      return SortedArrayHelpers.BinarySearch(_entries, key, _itemComparer);
    }

    private int ItemComparer(KeyValuePair<TKey, TValue> keyValuePair, TKey key) {
      return _comparer.Compare(keyValuePair.Key, key);
    }

    private class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey> {
      private readonly IReadOnlyMap<TKey, TValue> _dictionary;

      public KeyCollection(IReadOnlyMap<TKey, TValue> dictionary) {
        _dictionary = dictionary;
      }

      public IEnumerator<TKey> GetEnumerator() {
        return _dictionary.Select(x => x.Key).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }

      public void Add(TKey item) {
        throw new NotSupportedException();
      }

      public void Clear() {
        throw new NotSupportedException();
      }

      public bool Contains(TKey item) {
        // ReSharper disable once AssignNullToNotNullAttribute
        return _dictionary.ContainsKey(item);
      }

      public void CopyTo(TKey[] array, int arrayIndex) {
        foreach (var e in _dictionary) {
          array[arrayIndex++] = e.Key;
        }
      }

      public bool Remove(TKey item) {
        throw new NotSupportedException();
      }

      public void CopyTo(Array array, int index) {
        TKey[] array1 = array as TKey[];
        if (array1 != null) {
          CopyTo(array1, index);
        } else {
          object[] objArray = array as object[];
          if (objArray == null) {
            throw new ArgumentException("Invalid array", nameof(array));
          }

          foreach (var e in _dictionary) {
            objArray[index++] = e.Key;
          }
        }
      }

      public int Count {
        get { return _dictionary.Count; }
      }

      public object SyncRoot {
        get { return _dictionary.SyncRoot; }
      }
      public bool IsSynchronized {
        get { return false; }
      }
      public bool IsReadOnly {
        get { return true; }
      }
    }

    private class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue> {
      private readonly IReadOnlyMap<TKey, TValue> _dictionary;

      public ValueCollection(IReadOnlyMap<TKey, TValue> dictionary) {
        _dictionary = dictionary;
      }

      public IEnumerator<TValue> GetEnumerator() {
        return _dictionary.Select(x => x.Value).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }

      public void Add(TValue item) {
        throw new NotSupportedException();
      }

      public void Clear() {
        throw new NotSupportedException();
      }

      public bool Contains(TValue item) {
        foreach (var kvp in _dictionary) {
          if (Equals(kvp.Value, item)) {
            return true;
          }
        }
        return false;
      }

      public void CopyTo(TValue[] array, int arrayIndex) {
        foreach (var e in _dictionary) {
          array[arrayIndex++] = e.Value;
        }
      }

      public bool Remove(TValue item) {
        throw new NotSupportedException();
      }

      public void CopyTo(Array array, int index) {
        TValue[] array1 = array as TValue[];
        if (array1 != null) {
          CopyTo(array1, index);
        } else {
          object[] objArray = array as object[];
          if (objArray == null) {
            throw new ArgumentException("Invalid array", nameof(array));
          }

          foreach (var e in _dictionary) {
            objArray[index++] = e.Value;
          }
        }
      }

      public int Count {
        get { return _dictionary.Count; }
      }

      public object SyncRoot {
        get { return _dictionary.SyncRoot; }
      }
      public bool IsSynchronized {
        get { return false; }
      }
      public bool IsReadOnly {
        get { return true; }
      }
    }
  }
}