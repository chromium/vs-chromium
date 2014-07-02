// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// Wraps an IList{T} or {t}[] and expose a <see cref="CopyTo"/> method.
  /// </summary>
  public class ArrayWrapper<T> {
    private readonly int _count;
    private readonly T[] _sourceArray;
    private readonly List<T> _sourceList;

    public ArrayWrapper(IEnumerable<T> source) {
      if (source is List<T>) {
        _sourceList = (List<T>)source;
        _count = _sourceList.Count;
      } else {
        if (source is T[])
          _sourceArray = (T[])source;
        else
          _sourceArray = source.ToArray();
        _count = _sourceArray.Length;
      }
    }

    public int Count { get { return _count; } }

    public void CopyTo(int index, T[] array, int arrayIndex, int count) {
      if (_sourceList != null)
        _sourceList.CopyTo(index, array, arrayIndex, count);
      else
        Array.Copy(_sourceArray, index, array, arrayIndex, count);
    }
  }
}
