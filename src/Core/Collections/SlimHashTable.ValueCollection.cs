// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> {
    private class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue> {
      private readonly SlimHashTable<TKey, TValue> _dictionary;

      public ValueCollection(SlimHashTable<TKey, TValue> dictionary) {
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
        return _dictionary.ContainsValue(item);
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