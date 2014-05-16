// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  public class SortedArray {
    public static int BinarySearch<T, TKey>(IList<T> array, TKey item, Func<T, TKey, int> itemComparer) {
      return BinarySearch(array, 0, array.Count, item, itemComparer);
    }

    public static int BinarySearch<T, TKey>(IList<T> array, int index, int length, TKey item, Func<T, TKey, int> itemComparer) {
      var max = index + length - 1;
      var cur = 0;
      while (cur <= max) {
        var median = GetMedian(cur, max);

        var compareResult = itemComparer(array[median], item);
        if (compareResult < 0) {
          cur = median + 1;
        } else if (compareResult > 0) {
          max = median - 1;
        } else {
          return median;
        }
      }
      return ~cur;
    }

    private static int GetMedian(int low, int hi) {
      return low + (hi - low >> 1);
    }
  }

  public class SortedArray<T> : IList<T>, ICollection<T> {
    private readonly T[] _items;

    public SortedArray() {
      _items = new T[0];
    }

    /// <summary>
    /// Note: Takes ownership of <paramref name="items"/> and assumes they are sorted.
    /// </summary>
    public SortedArray(T[] items) {
      _items = items;
    }

    public IEnumerator<T> GetEnumerator() {
      IEnumerable<T> items = _items;
      return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return this.GetEnumerator();
    }

    public int BinaraySearch<TKey>(TKey item, Func<T, TKey, int> itemComparer) {
      return SortedArray.BinarySearch(_items, 0, _items.Length, item, itemComparer);
    }

    public void Add(T item) {
      throw IsReadOnlyException();
    }

    public void Clear() {
      throw IsReadOnlyException();
    }

    public bool Contains(T item) {
      return _items.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex) {
      _items.CopyTo(array, arrayIndex);
    }

    public bool Remove(T item) {
      throw IsReadOnlyException();
    }

    private static Exception IsReadOnlyException() {
      return new InvalidOperationException("SortedArray<T> is a read only collection.");
    }

    public int Count { get { return _items.Length; } }

    public bool IsReadOnly { get { return true; } }
    public int IndexOf(T item) {
      IList<T> items = _items;
      return items.IndexOf(item);
    }

    public void Insert(int index, T item) {
      throw IsReadOnlyException();
    }

    public void RemoveAt(int index) {
      throw IsReadOnlyException();
    }

    public T this[int index] { 
      get { return _items[index]; }
      set {
        throw  IsReadOnlyException();
      }
    }
  }
}