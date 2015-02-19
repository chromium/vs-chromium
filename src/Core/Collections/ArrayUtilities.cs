// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Diagnostics;
using VsChromium.Core.Linq;

namespace VsChromium.Core.Collections {
  public static class ArrayUtilities {
    private const int SmallArrayThreshold = 200;

    public static ArrayDiffsResult<T> BuildArrayDiffs<T>(
      IList<T> leftList,
      IList<T> rightList,
      IEqualityComparer<T> comparer) {

      bool smallList = leftList.Count + rightList.Count <= SmallArrayThreshold;
      var result = smallList
        ? BuildArrayDiffsForSmallArrays(leftList, rightList, comparer)
        : BuildArrayDiffsForLargeArrays(leftList, rightList, comparer);

      // Quick check assumption about identity is verified: both list should not
      // contain duplicate elements.
      Debug.Assert(result.LeftOnlyItems.Count + result.RightOnlyItems.Count + result.CommonItems.Count * 2 == leftList.Count + rightList.Count);
      return result;
    }

    public static ArrayDiffsResult<T> BuildArrayDiffsForSmallArrays<T>(
      IList<T> leftList,
      IList<T> rightList) {
      return BuildArrayDiffsForSmallArrays(leftList, rightList, null);
    }

    public static ArrayDiffsResult<T> BuildArrayDiffsForSmallArrays<T>(
      IList<T> leftList,
      IList<T> rightList,
      IEqualityComparer<T> comparer) {
      comparer = comparer ?? EqualityComparer<T>.Default;

      var result = new ArrayDiffsResult<T> {
        LeftOnlyItems = new List<T>(),
        RightOnlyItems = new List<T>(),
        CommonItems = new List<LeftRightItemPair<T>>()
      };

      // Append left items, either unique or common with right items
      foreach (var left in leftList) {
        var index = ListIndexOf(rightList, left, comparer);
        if (index >= 0) {
          result.CommonItems.Add(new LeftRightItemPair<T>(left, rightList[index]));
        } else {
          result.LeftOnlyItems.Add(left);
        }
      }

      // Append right items (unique ones only)
      foreach (var right in rightList) {
        var index = ListIndexOf(leftList, right, comparer);
        if (index < 0) {
          result.RightOnlyItems.Add(right);
        }
      }
      
      return result;
    }

    public static ArrayDiffsResult<T> BuildArrayDiffsForLargeArrays<T>(
      IList<T> leftList,
      IList<T> rightList) {
      return BuildArrayDiffsForLargeArrays(leftList, rightList, null);
    }

    public static ArrayDiffsResult<T> BuildArrayDiffsForLargeArrays<T>(
      IList<T> leftList,
      IList<T> rightList,
      IEqualityComparer<T> comparer) {
      comparer = comparer ?? EqualityComparer<T>.Default;

      var result = new ArrayDiffsResult<T> {
        LeftOnlyItems = new List<T>(),
        RightOnlyItems = new List<T>(),
        CommonItems = new List<LeftRightItemPair<T>>()
      };

      // Build both map from item to item index.
      var leftMap = new Dictionary<T, int>(leftList.Count, comparer);
      leftList.ForAll((index, x) => leftMap.Add(x, index));

      var rightMap = new Dictionary<T, int>(leftList.Count, comparer);
      rightList.ForAll((index, x) => rightMap.Add(x, index));

      // Append left items, either unique or common with right items
      foreach (var left in leftList) {
        var rightIndex = MapIndexOf(rightMap, left);
        if (rightIndex >= 0) {
          result.CommonItems.Add(new LeftRightItemPair<T>(left, rightList[rightIndex]));
        } else {
          result.LeftOnlyItems.Add(left);
        }
      }

      // Append right items (unique ones only)
      foreach (var right in rightList) {
        var leftIndex = MapIndexOf(leftMap, right);
        if (leftIndex < 0) {
          result.RightOnlyItems.Add(right);
        }
      }

      return result;
    }

    /// <summary>
    /// Returns index of <paramref name="item"/> in <paramref name="items"/>
    /// collection, using <paramref name="comparer"/> equality comparer. Returns
    /// -1 if <paramref name="item"/> is not present.
    /// </summary>
    private static int ListIndexOf<T>(IList<T> items, T item, IEqualityComparer<T> comparer) {
      for (var i = 0; i < items.Count; i++) {
        if (comparer.Equals(items[i], item))
          return i;
      }
      return -1;
    }

    private static int MapIndexOf<T>(IDictionary<T, int> items, T item) {
      int result;
      if (!items.TryGetValue(item, out result))
        return -1;
      Debug.Assert(result >= 0);
      return result;
    }
  }
}