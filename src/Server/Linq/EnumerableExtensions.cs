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
        yield return Utility.KeyValuePair.Create(index, size);
        index += size;
      }
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

    public static IEnumerable<TSource> OrderByDescendingThenTake<TSource, TKey>(this IEnumerable<TSource> source, int count, Func<TSource, TKey> keySelector) {
      var heap = new MinHeap<TSource>(count + 1, new OrderByComparer<TSource, TKey>(keySelector));
      return OrderByThenTakeImpl<TSource, TKey>(source, count, heap);
    }

    private static IEnumerable<TSource> OrderByThenTakeImpl<TSource, TKey>(IEnumerable<TSource> source, int count, IHeap<TSource> heap) {
      foreach (var item in source) {
        heap.Add(item);
        if (heap.Count > count) {
          // Remove minimum/maximum value
          heap.Remove();
        }
      }

      return heap.ConsumeHeap().Reverse();
    }

    private static IEnumerable<TSource> ConsumeHeap<TSource>(this IHeap<TSource> heap) {
      while (heap.Count > 0) {
        yield return heap.Remove();
      }
    }

    public class OrderByComparer<TSource, TKey> : IComparer<TSource> {
      private readonly Func<TSource, TKey> _keySelector;
      private readonly Comparer<TKey> _comparer;

      public OrderByComparer(Func<TSource, TKey> keySelector) {
        _keySelector = keySelector;
        _comparer = Comparer<TKey>.Default;
      }

      public int Compare(TSource x, TSource y) {
        return _comparer.Compare(_keySelector(x), _keySelector(y));
      }
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
