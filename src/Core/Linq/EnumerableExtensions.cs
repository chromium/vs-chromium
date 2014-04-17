// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VsChromium.Core.Collections;

namespace VsChromium.Core.Linq {
  public static class EnumerableExtensions {
    public static IEnumerable<int> GetPartitionSizes<TSource>(this IList<TSource> source, int partitionCount) {
      if (partitionCount <= 0)
        throw new ArgumentException("Invalid count", "partitionCount");

      var baseSize = source.Count / partitionCount;
      var additionalItems = source.Count % partitionCount;

      return Enumerable
        .Range(0, partitionCount)
        .Select(i => {
          var actualSize = baseSize;
          if (additionalItems > 0) {
            additionalItems--;
            actualSize++;
          }
          return actualSize;
        });
    }

    public static IList<IList<TSource>> CreatePartitions<TSource>(this IList<TSource> source, int partitionCount) {
      var sourceIndex = 0;
      return GetPartitionSizes(source, partitionCount).
        Select(size => {
          IList<TSource> result = new ListSegment<TSource>(source, sourceIndex, size);
          sourceIndex += size;
          return result;
        }).ToList();
    }

    public static void ForAll<TSource>(this IEnumerable<TSource> source, Action<TSource> action) {
      foreach (var x in source) {
        action(x);
      }
    }

    public static void ForAll<TSource>(this IEnumerable<TSource> source, Action<int, TSource> action) {
      var index = 0;
      foreach (var x in source) {
        action(index, x);
        index++;
      }
    }

    public static ReadOnlyCollection<TSource> ToReadOnlyCollection<TSource>(this IEnumerable<TSource> source) {
      return new ReadOnlyCollection<TSource>(source.ToArray());
    }

    public static SortedSet<TSource> ToSortedSet<TSource>(this IEnumerable<TSource> source) {
      return new SortedSet<TSource>(source);
    }

    public static SortedSet<TSource> ToSortedSet<TSource>(this IEnumerable<TSource> source, IComparer<TSource> comparer) {
      return new SortedSet<TSource>(source, comparer);
    }

    public static bool IsEmpty<TSource>(this Stack<TSource> stack ) {
      return stack.Count == 0;
    }
  }
}
