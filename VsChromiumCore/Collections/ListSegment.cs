// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;

namespace VsChromiumCore.Collections {
  public struct ListSegment<T> : IList<T> {
    private readonly int _count;
    private readonly IList<T> _list;
    private readonly int _offset;

    public ListSegment(IList<T> list, int offset, int count) {
      this._list = list;
      this._offset = offset;
      this._count = count;
    }

    public int Offset {
      get {
        return this._offset;
      }
    }

    public int Count {
      get {
        return this._count;
      }
    }

    public T this[int index] {
      get {
        if (index < 0 || index >= this._count) {
          throw new ArgumentOutOfRangeException("index");
        }
        return this._list[this._offset + index];
      }

      set {
        throw new NotImplementedException();
      }
    }

    public bool IsReadOnly {
      get {
        return true;
      }
    }

    public int IndexOf(T item) {
      for (var i = 0; i < this._count; i++)
        if (Equals(this[i], item))
          return i;
      return -1;
    }

    public void Insert(int index, T item) {
      throw new NotSupportedException();
    }

    public void RemoveAt(int index) {
      throw new NotSupportedException();
    }

    public void Add(T item) {
      throw new NotSupportedException();
    }

    public void Clear() {
      throw new NotSupportedException();
    }

    public bool Contains(T item) {
      return IndexOf(item) >= 0;
    }

    public void CopyTo(T[] array, int arrayIndex) {
      for (var i = 0; i < this._count; i++)
        array[arrayIndex + i] = this[i];
    }

    public bool Remove(T item) {
      throw new NotSupportedException();
    }

    public IEnumerator<T> GetEnumerator() {
      for (var i = 0; i < Count; i++)
        yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }
  }
}
