// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections;
using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemDatabase {
  public class FileNameDictionary<TElement> : IEnumerable<KeyValuePair<FileName, TElement>> {
    private readonly IDictionary<FileName, TElement> _items = new Dictionary<FileName, TElement>();

    public FileNameDictionary(IDictionary<FileName, TElement> files) {
      _items = files;
    }

    public IEnumerator<KeyValuePair<FileName, TElement>> GetEnumerator() {
      return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return this.GetEnumerator();
    }

    public TElement this[FileName key] {
      get { return _items[key]; }
    }

    public int Count {
      get { return _items.Count; }
    }

    public IEnumerable<TElement> Values { get { return _items.Values; } }
  }
}
