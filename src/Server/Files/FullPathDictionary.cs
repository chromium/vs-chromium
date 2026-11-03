// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Files {
  public class FullPathDictionary<T> : IEnumerable<KeyValuePair<FullPath, T>> {
    private readonly Dictionary<FullPath, T> _entries = new Dictionary<FullPath, T>();

    public IEnumerator<KeyValuePair<FullPath, T>> GetEnumerator() {
      return _entries.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    public void Add(FullPath name, T value) {
      _entries[name] = value;
    }

    public bool Contains(FullPath name) {
      return _entries.ContainsKey(name);
    }

    public void Clear() {
      _entries.Clear();
    }

    public T Get(FullPath name) {
      T result;
      if (_entries.TryGetValue(name, out result))
        return result;
      return default(T);
    }

    public bool TryGet(FullPath name, out T value) {
      return _entries.TryGetValue(name, out value);
    }
  }
}
