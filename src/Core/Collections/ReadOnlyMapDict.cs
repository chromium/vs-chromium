// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  public class ReadOnlyMapDict<TKey, TValue> : IReadOnlyMap<TKey, TValue> {
    private readonly IDictionary<TKey, TValue> _dictionary;
    private readonly object _syncRoot = new object();

    public ReadOnlyMapDict(IDictionary<TKey, TValue> dictionary) {
      _dictionary = dictionary;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      return _dictionary.GetEnumerator();
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
      return _dictionary.Contains(item);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      _dictionary.CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
      throw new NotSupportedException();
    }

    public int Count {
      get { return _dictionary.Count; }
    }

    public bool IsReadOnly {
      get { return true; }
    }

    public TValue this[TKey key] {
      get { return _dictionary[key]; }
    }

    public object SyncRoot {
      get { return _syncRoot; }
    }

    public bool TryGetValue(TKey key, out TValue value) {
      return _dictionary.TryGetValue(key, out value);
    }

    public bool ContainsKey(TKey key) {
      return _dictionary.ContainsKey(key);
    }

    public ICollection<TKey> Keys {
      get { return _dictionary.Keys; }
    }

    public ICollection<TValue> Values {
      get { return _dictionary.Values; }
    }
  }
}