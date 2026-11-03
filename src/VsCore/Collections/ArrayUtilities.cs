// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Collections {
  public static class ArrayUtilities {
    private const int SmallArrayThreshold = 200;

    public class EmptyList<T> {
      public static IList<T> Instance = new List<T>().ToReadOnlyCollection();
    }

    public static ArrayDiffsResult<T> BuildArrayDiffs<T>(IList<T> leftList, IList<T> rightList) {
      return BuildArrayDiffs(leftList, rightList, null);
    }

    public static ArrayDiffsResult<T> BuildArrayDiffs<T>(
      IList<T> leftList,
      IList<T> rightList,
      IEqualityComparer<T> comparer) {
      comparer = comparer ?? EqualityComparer<T>.Default;

      var result = ProcessSpecialCases(leftList, rightList, comparer);
      if (result != null) {
        return result.Value;
      }

      bool smallList = leftList.Count + rightList.Count <= SmallArrayThreshold;
      var result2 = smallList
        ? BuildArrayDiffsForSmallArrays(leftList, rightList, comparer)
        : BuildArrayDiffsForLargeArrays(leftList, rightList, comparer);

      // Quick check assumption about identity is verified: both list should not
      // contain duplicate elements.
      Invariants.Assert(
        result2.LeftOnlyItems.Count +
        result2.RightOnlyItems.Count +
        result2.CommonItems.Count * 2 ==
        leftList.Count + rightList.Count);
      return result2;
    }

    private static class EmptyResult<T> {
      public static ArrayDiffsResult<T> Instance = new ArrayDiffsResult<T>(
        EmptyList<T>.Instance,
        EmptyList<T>.Instance,
        EmptyList<LeftRightItemPair<T>>.Instance);
    }

    private static ArrayDiffsResult<T>? ProcessSpecialCases<T>(IList<T> leftList, IList<T> rightList, IEqualityComparer<T> comparer) {
      if (leftList.Count == 0 && rightList.Count == 0) {
        return EmptyResult<T>.Instance;
      }

      if (leftList.Count == 0) {
        return new ArrayDiffsResult<T>(
          EmptyList<T>.Instance,
          rightList,
          EmptyList<LeftRightItemPair<T>>.Instance);
      }

      if (rightList.Count == 0) {
        return new ArrayDiffsResult<T>(
          leftList,
          EmptyList<T>.Instance,
          EmptyList<LeftRightItemPair<T>>.Instance);
      }

      if (ListAreEqual(leftList, rightList, comparer)) {
        var common = new LeftRightItemPair<T>[rightList.Count];
        for (var i = 0; i < common.Length; i++) {
          common[i] = new LeftRightItemPair<T>(leftList[i], rightList[i]);
        }

        return new ArrayDiffsResult<T>(EmptyList<T>.Instance, EmptyList<T>.Instance, common);
      }

      return null;
    }

    private static bool ListAreEqual<T>(IList<T> leftList, IList<T> rightList, IEqualityComparer<T> comparer) {
      if (leftList.Count != rightList.Count)
        return false;

      for (var i = 0; i < leftList.Count; i++) {
        if (!comparer.Equals(leftList[i], rightList[i]))
          return false;
      }
      return true;
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

      var result = new ArrayDiffsResult<T>(
        new List<T>(),
        new List<T>(),
        new List<LeftRightItemPair<T>>()
      );

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

      var result = new ArrayDiffsResult<T>(
        new List<T>(),
        new List<T>(),
        new List<LeftRightItemPair<T>>()
      );

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
      Invariants.Assert(result >= 0);
      return result;
    }

    public static void RemoveDuplicates<T>(IList<T> sortedList, Func<T, T, bool> comparer) {
      if (sortedList.Count <= 1)
        return;

      var previousItem = sortedList[0];
      for (var i = 1; i < sortedList.Count; ) {
        var item = sortedList[i];
        if (comparer(previousItem, item)) {
          sortedList.RemoveAt(i);
          continue;
        }
        previousItem = item;
        i++;
      }
    }


  }
}