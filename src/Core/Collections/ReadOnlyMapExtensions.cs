// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  public static class ReadOnlyMapExtensions {
    public static IReadOnlyMap<TKey, TValue> ToReadOnlyMap<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) where TKey: IComparable<TKey> {
      return ToReadOnlyMapDict(dictionary);
      //return ToReadOnlyMapSortedArray(dictionary);
    }

    public static IReadOnlyMap<TKey, TValue> ToReadOnlyMapDict<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) {
      return new ReadOnlyMapDict<TKey, TValue>(dictionary);
    }

    public static IReadOnlyMap<TKey, TValue> ToReadOnlyMapSortedArray<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) where TKey : IComparable<TKey> {
      var list = dictionary.ToArray();
      Array.Sort(list, (x, y) => x.Key.CompareTo(y.Key));
      return new ReadOnlyMapSortedList<TKey, TValue>(list);
    }
  }
}