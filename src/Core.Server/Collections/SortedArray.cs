using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// Wraps a sorted array of <typeparamref name="T"/> elements as a read only
  /// collection, implements IList&lt;<typeparamref name="T"/>&gt; and exposing
  /// a <see cref="BinarySearch{TKey}"/> method.
  /// </summary>
  public class SortedArray<T> : IList<T> {
    private readonly T[] _items;

    public SortedArray() {
      _items = new T[0];
    }

    /// <summary>
    /// Note: Takes ownership of <paramref name="items"/> and assumes they are sorted.
    /// </summary>
    public SortedArray(T[] items) {
      _items = items;
    }

    public IEnumerator<T> GetEnumerator() {
      IEnumerable<T> items = _items;
      return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return this.GetEnumerator();
    }

    public int BinarySearch<TValue>(TValue value, Func<T, TValue, int> valueComparer) {
      return SortedArrayHelpers.BinarySearch(_items, 0, _items.Length, value, valueComparer);
    }

    public void Add(T item) {
      throw IsReadOnlyException();
    }

    public void Clear() {
      throw IsReadOnlyException();
    }

    public bool Contains(T item) {
      return _items.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex) {
      _items.CopyTo(array, arrayIndex);
    }

    public bool Remove(T item) {
      throw IsReadOnlyException();
    }

    private static Exception IsReadOnlyException() {
      return new InvalidOperationException("SortedArray<T> is a read only collection.");
    }

    public int Count { get { return _items.Length; } }

    public bool IsReadOnly { get { return true; } }
    public int IndexOf(T item) {
      IList<T> items = _items;
      return items.IndexOf(item);
    }

    public void Insert(int index, T item) {
      throw IsReadOnlyException();
    }

    public void RemoveAt(int index) {
      throw IsReadOnlyException();
    }

    public T this[int index] { 
      get { return _items[index]; }
      set {
        throw  IsReadOnlyException();
      }
    }
  }
}