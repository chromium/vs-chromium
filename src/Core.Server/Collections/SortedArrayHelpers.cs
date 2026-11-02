// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// Exposed helper function on sorted arrays.
  /// </summary>
  public class SortedArrayHelpers {
    /// <summary>
    /// Implements a binary search similar to <see
    /// cref="Array.BinarySearch(Array, object, System.Collections.IComparer)"/>,
    /// except that the comparer can be used to compare array elements with some arbitrary search value.
    /// </summary>
    public static int BinarySearch<T, TKey>(IList<T> array, TKey item, Func<T, TKey, int> itemComparer) {
      return BinarySearch(array, 0, array.Count, item, itemComparer);
    }

    /// <summary>
    /// Implements a binary search similar to <see
    /// cref="Array.BinarySearch(Array, int, int, object, System.Collections.IComparer)"/>,
    /// except that the comparer can be used to compare array elements with some arbitrary search value.
    /// </summary>
    public static int BinarySearch<TElement, TValue>(IList<TElement> array, int index, int length, TValue value, Func<TElement, TValue, int> valueComparer) {
      var max = index + length - 1;
      var cur = 0;
      while (cur <= max) {
        var median = GetMedian(cur, max);

        var compareResult = valueComparer(array[median], value);
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
}