// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Collections;

namespace VsChromium.Core.Linq {
  public static class PLinqExtensions {
    public static IEnumerable<IList<TSource>> PartitionEvenly<TSource>(
      this IList<TSource> source,
      Func<TSource, long> weight) {
      return PartitionEvenly(source, weight, Environment.ProcessorCount);
    }

    public static IEnumerable<IList<TSource>> PartitionEvenly<TSource>(
      this IList<TSource> source,
      Func<TSource, long> weight,
      int partitionCount) {
      var sw = Stopwatch.StartNew();

      var partitions = source
        .GetPartitionSizes(partitionCount)
        .Select(x => new Partition<TSource> {
          MaxCount = x,
          Items = new List<TSource>(x)
        })
        .ToList();

      // Create MinHeap of partitions (wrt Weight function).
      var minHeap = new MinHeap<Partition<TSource>>(new PartitionWeightComparer<TSource>());
      partitions.ForAll(x => minHeap.Add(x));

      // Distribute items using MinHeap.
      source
        // Sort so that elements with highest weight are first, so that we have a better chance
        // of ending with partitions of same total weight - it is easier to adjust with elements of
        // low weight than with elements of high weight.
        .OrderByDescending(x => weight(x))
        .ForAll(item => {
          var min = minHeap.Remove();
          min.Items.Add(item);
          min.TotalWeight += weight(item);
          if (min.Items.Count < min.MaxCount)
            minHeap.Add(min);
        });

      sw.Stop();
#if false
      Logger.Log("Created {0} partitions for a total ot {1:n0} elements and weight of {2:n0} in {3:n0} msec.",
        partitions.Count,
        partitions.Aggregate(0L, (c, x) => c + x.Items.Count),
        partitions.Aggregate(0L, (c, x) => c + x.TotalWeight),
        sw.ElapsedMilliseconds);
      partitions.ForAll(x => Logger.Log("  Partition count is {0:n0}, total weight is {1:n0}.", x.Items.Count, x.TotalWeight));
#endif
      return partitions.Select(x => x.Items);
    }

    private class Partition<T> {
      public int MaxCount { get; set; }
      public List<T> Items { get; set; }
      public long TotalWeight { get; set; }
    }

    private class PartitionWeightComparer<T> : IComparer<Partition<T>> {
      public int Compare(Partition<T> x, Partition<T> y) {
        return x.TotalWeight.CompareTo(y.TotalWeight);
      }
    }
  }
}
