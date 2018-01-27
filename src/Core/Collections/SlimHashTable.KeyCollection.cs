// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> {
    private class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey> {
      private readonly SlimHashTable<TKey, TValue> _dictionary;

      public KeyCollection(SlimHashTable<TKey, TValue> dictionary) {
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
  }
}