// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VsChromiumCore.FileNames {
  public class FullPathNameSet<T> : IEnumerable<KeyValuePair<FullPathName, T>> {
    private readonly Dictionary<FullPathName, T> _entries = new Dictionary<FullPathName,T>();

    public IEnumerator<KeyValuePair<FullPathName,T>> GetEnumerator() {
      return this._entries.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    public void Add(FullPathName name, T value) {
      this._entries[name] = value;
    }

    public bool Contains(FullPathName name) {
      return this._entries.ContainsKey(name);
    }

    public int RemoveWhere(Predicate<FullPathName> match) {
      var keys = this._entries.Where(x => match(x.Key)).ToList();
      foreach (var key in keys) {
        this._entries.Remove(key.Key);
      }
      return keys.Count;
    }

    public void Clear() {
      this._entries.Clear();
    }

    public T Get(FullPathName name) {
      T result;
      if (this._entries.TryGetValue(name, out result))
        return result;
      return default(T);
    }

    public bool TryGet(FullPathName name, out T value) {
      return this._entries.TryGetValue(name, out value);
    }
  }
}
