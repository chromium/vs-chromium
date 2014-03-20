// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.FileNames {
  public class FullPathNameSet<T> : IEnumerable<KeyValuePair<FullPathName, T>> {
    private readonly Dictionary<FullPathName, T> _entries = new Dictionary<FullPathName, T>();

    public IEnumerator<KeyValuePair<FullPathName, T>> GetEnumerator() {
      return _entries.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    public void Add(FullPathName name, T value) {
      _entries[name] = value;
    }

    public bool Contains(FullPathName name) {
      return _entries.ContainsKey(name);
    }

    public int RemoveWhere(Predicate<FullPathName> match) {
      var keys = _entries.Where(x => match(x.Key)).ToList();
      foreach (var key in keys) {
        _entries.Remove(key.Key);
      }
      return keys.Count;
    }

    public void Clear() {
      _entries.Clear();
    }

    public T Get(FullPathName name) {
      T result;
      if (_entries.TryGetValue(name, out result))
        return result;
      return default(T);
    }

    public bool TryGet(FullPathName name, out T value) {
      return _entries.TryGetValue(name, out value);
    }
  }
}
