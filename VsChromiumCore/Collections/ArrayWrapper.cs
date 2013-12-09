// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VsChromiumCore.Collections {
  public class ArrayWrapper<T> {
    private readonly int _count;
    private readonly T[] _sourceArray;
    private readonly List<T> _sourceList;

    public ArrayWrapper(IEnumerable<T> source) {
      if (source is List<T>) {
        this._sourceList = (List<T>)source;
        this._count = this._sourceList.Count;
      } else {
        if (source is T[])
          this._sourceArray = (T[])source;
        else
          this._sourceArray = source.ToArray();
        this._count = this._sourceArray.Length;
      }
    }

    public int Count {
      get {
        return this._count;
      }
    }

    public void CopyTo(int index, T[] array, int arrayIndex, int count) {
      if (this._sourceList != null)
        this._sourceList.CopyTo(index, array, arrayIndex, count);
      else
        Array.Copy(this._sourceArray, index, array, arrayIndex, count);
    }
  }
}
