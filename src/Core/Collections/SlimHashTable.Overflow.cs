// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> {
    private class Overflow {
      private readonly int _capacity;
      private readonly List<Entry> _entries;
      private int _freeListHead;
      private int _freeListSize;

      public Overflow(int capacity) {
        _capacity = capacity;
        _entries = new List<Entry>(capacity);
        _freeListHead = -1;
      }

      public int Capacity => _capacity;
      public int Count => _entries.Count;

      public Entry Get(int index) {
        return _entries[index];
      }

      public void Free(int index) {
        _entries[index] = new Entry(default(TValue), _freeListHead);
        _freeListHead = index;
        _freeListSize++;
      }

      public void SetOverflowIndex(int index, int overflowIndex) {
        _entries[index] = new Entry(_entries[index].Value, overflowIndex);
      }

      public int Allocate() {
        if (_freeListHead < 0) {
          Invariants.Assert(_freeListSize == 0);
          Grow();
        }

        var index = _freeListHead;
        _freeListHead = _entries[index].OverflowIndex;
        _entries[index] = default(Entry);
        _freeListSize--;
        return index;
      }

      private void Grow() {
        var oldLen = _entries.Count;
        var newLen = Math.Max(2, _entries.Count * 2);
        for (var i = oldLen; i < newLen; i++) {
          var nextIndex = (i + 1 < newLen) ? i + 1 : -1;
          _entries.Add(new Entry(default(TValue), nextIndex));
        }
        _freeListHead = oldLen;
        _freeListSize += newLen - oldLen;
      }

      public void Set(int index, Entry entry) {
        _entries[index] = entry;
      }
    }
  }
}