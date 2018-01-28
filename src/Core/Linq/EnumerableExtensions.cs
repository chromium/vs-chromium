// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using VsChromium.Core.Collections;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Linq {
  public static class EnumerableExtensions {
    public struct ListEnumerable<T> {
      private readonly IList<T> _list;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public ListEnumerable(IList<T> list) {
        _list = list;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public ListEnumerator<T> GetEnumerator() {
        return new ListEnumerator<T>(_list);
      }
    }

    public struct ListEnumerator<T> {
      private readonly IList<T> _list;
      private readonly int _count;
      private int _index;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public ListEnumerator(IList<T> list) {
        _list = list;
        _count = list.Count;
        _index = -1;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool MoveNext() {
        return (++_index < _count);
      }

      public T Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return _list[_index]; }
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ListEnumerable<T> ToForeachEnum<T>(this IList<T> list) {
      return new ListEnumerable<T>(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<T> ToForeachEnum<T>(this List<T> list) {
      return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] ToForeachEnum<T>(this T[] list) {
      return list;
    }

    /// <summary>
    /// Partition a list of elements of <paramref name="weight"/> into <paramref
    /// name="partitionCount"/> lists having identical total weight (as best as
    /// possible).
    /// </summary>
    public static IEnumerable<IList<TSource>> PartitionWithWeight<TSource>(this IList<TSource> source, Func<TSource, long> weight, int partitionCount) {
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
        // Sort so that elements with highest weight are first, so that we
        // have a better chance of ending with partitions of same total weight
        // - it is easier to adjust with elements of low weight than with
        // elements of high weight.
        .OrderByDescending(weight)
        .ForAll(item => {
          var min = minHeap.Remove();
          min.Items.Add(item);
          min.TotalWeight += weight(item);
          if (min.Items.Count < min.MaxCount)
            minHeap.Add(min);
        });

#if false
      Logger.LogInfo("Created {0} partitions for a total ot {1:n0} elements and weight of {2:n0}.",
        partitions.Count,
        partitions.Aggregate(0L, (c, x) => c + x.Items.Count),
        partitions.Aggregate(0L, (c, x) => c + x.TotalWeight));
      partitions.ForAll(x => {
        Logger.LogInfo("  Partition count is {0:n0}, total weight is {1:n0}.", x.Items.Count, x.TotalWeight);
      });
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
        Invariants.Assert(x != null);
        Invariants.Assert(y != null);
        return x.TotalWeight.CompareTo(y.TotalWeight);
      }
    }

    public static IEnumerable<int> GetPartitionSizes<TSource>(this IList<TSource> source, int partitionCount) {
      return GetPartitionSizes(source.Count, partitionCount);
    }

    public static IEnumerable<int> GetPartitionSizes(int count, int partitionCount) {
      if (partitionCount <= 0)
        throw new ArgumentException("Invalid count", "partitionCount");

      var baseSize = count / partitionCount;
      var additionalItems = count % partitionCount;

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

    public static IEnumerable<KeyValuePair<int, int>> GetPartitionRanges<TSource>(
      this IList<TSource> source,
      int partitionCount) {
      return GetPartitionRanges(source.Count, partitionCount);
    }

    public static IEnumerable<KeyValuePair<int, int>> GetPartitionRanges(int count, int partitionCount) {
      var index = 0;
      foreach (var size in GetPartitionSizes(count, partitionCount)) {
        yield return KeyValuePair.Create(index, size);
        index += size;
      }
    }

    public static IList<IList<TSource>> PartitionByChunks<TSource>(this IList<TSource> source, int partitionCount) {
      var sourceIndex = 0;
      return GetPartitionSizes(source, partitionCount).Select(size => {
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

    public static IEnumerable<T> ConcatSingle<T>(this IEnumerable<T> source, T item) {
      return source.Concat(Single(item));
    }

    public static IEnumerable<T> ConcatSingle<T>(this IEnumerable<T> source, T item, Func<bool> filter) {
      if (filter())
        return source.ConcatSingle(item);
      else
        return source;
    }

    public static IEnumerable<T> Single<T>(T item) {
      return Enumerable.Repeat(item, 1);
    }

    public static ReadOnlyCollection<TSource> ToReadOnlyCollection<TSource>(this IEnumerable<TSource> source) {
      var coll = source as ReadOnlyCollection<TSource>;
      if (coll != null)
        return coll;

      var list = source as IList<TSource>;
      if (list != null) {
        if (list.Count == 0) {
          return EmptyCollection<TSource>.Instance;
        }
        return new ReadOnlyCollection<TSource>(list);
      }

      list = source.ToArray();
      if (list.Count == 0) {
        return EmptyCollection<TSource>.Instance;
      }
      return new ReadOnlyCollection<TSource>(list);
    }

    public static IList<TSource> ToReadOnlyList<TSource>(this IEnumerable<TSource> source) {
      var coll = source as ReadOnlyCollection<TSource>;
      if (coll != null)
        return coll;

      var list = source as IList<TSource>;
      if (list != null) {
        if (list.Count == 0) {
          return EmptyCollection<TSource>.Instance;
        }
        return list;
      }

      list = source.ToArray();
      if (list.Count == 0) {
        return EmptyCollection<TSource>.Instance;
      }
      return list;
    }

    private static class EmptyCollection<TSource> {
      public static readonly ReadOnlyCollection<TSource> Instance = new ReadOnlyCollection<TSource>(new TSource[0]);
    }

    /// <summary>
    /// Returns <code>null</code> when not found.
    /// </summary>
    public static TValue GetValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : class {
      return dictionary.GetValue(key, null);
    }

    /// <summary>
    /// Returns <code>defaultValue</code> when not found.
    /// </summary>
    public static TValue GetValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue) {
      TValue result;
      if (!dictionary.TryGetValue(key, out result))
        return defaultValue;
      return result;
    }

    /// <summary>
    /// Returns <code>defaultValue</code> when not found.
    /// </summary>
    public static TValue? GetValueType<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : struct {
      TValue result;
      if (!dictionary.TryGetValue(key, out result))
        return null;
      return result;
    }

    /// <summary>
    /// Returns <code>defaultValue</code> when not found.
    /// </summary>
    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value) {
      TValue result;
      if (dictionary.TryGetValue(key, out result)) {
        return result;
      }

      dictionary[key] = value;
      return value;
    }

    /// <summary>
    /// Returns <code>null</code> when not found.
    /// </summary>
    public static TValue GetValue<TKey, TValue>(this IReadOnlyMap<TKey, TValue> dictionary, TKey key) where TValue : class {
      return dictionary.GetValue(key, null);
    }

    /// <summary>
    /// Returns <code>defaultValue</code> when not found.
    /// </summary>
    public static TValue GetValue<TKey, TValue>(this IReadOnlyMap<TKey, TValue> dictionary, TKey key, TValue defaultValue) {
      TValue result;
      if (!dictionary.TryGetValue(key, out result))
        return defaultValue;
      return result;
    }

    public static void RemoveWhere<TKey, TValue>(
      this IDictionary<TKey, TValue> source,
      Func<KeyValuePair<TKey, TValue>, bool> predicate) {
      var toRemove = source.Where(predicate).ToList();
      foreach (var x in toRemove) {
        source.Remove(x.Key);
      }
    }
  }
}
