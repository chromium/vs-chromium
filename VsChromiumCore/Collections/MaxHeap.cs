// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromiumCore.Collections {
  public class MaxHeap<T> : IHeap<T> {
    private const int _defaultCapacity = 6;

    private readonly IComparer<T> _comparer;
    private int _count;
    private T[] _items;

    public MaxHeap()
      : this(_defaultCapacity, null) {
    }

    public MaxHeap(int capacity)
      : this(capacity, null) {
    }

    public MaxHeap(int capacity, IComparer<T> comparer) {
      _items = new T[capacity];
      _count = 0;
      _comparer = (comparer ?? Comparer<T>.Default);
    }

    public MaxHeap(IComparer<T> comparer)
      : this(_defaultCapacity, comparer) {
    }

    public T Max { get { return Root; } }

    public int Count { get { return _count; } }

    public T Root {
      get {
        if (_count == 0)
          throw new InvalidOperationException("Heap is empty.");

        return _items[0];
      }
    }

    public T Remove() {
      if (_count == 0)
        throw new InvalidOperationException("Heap is empty.");

      var result = Root;
      var last = _count - 1;
      Swap(0, last);
      _items[_count - 1] = default(T);
      _count--;
      SiftDown(0);
      return result;
    }

    public void Add(T value) {
      ExpandArray();
      var leaf = _count;
      _items[leaf] = value;
      _count++;
      SiftUp(leaf);
    }

    public void Clear() {
      Array.Clear(_items, 0, _items.Length);
      _count = 0;
    }

    private void SiftDown(int root) {
      var child = LeftChild(root);
      while (child < _count) {
        var rightChild = RightChildFromLeftChild(child);
        if (rightChild < _count && Compare(child, rightChild) < 0)
          child = rightChild;
        if (Compare(root, child) < 0)
          Swap(root, child);
        else
          return;

        root = child;
        child = LeftChild(root);
      }
    }

    private void SiftUp(int child) {
      while (child > 0) {
        var parent = Parent(child);
        if (Compare(parent, child) >= 0)
          break;
        Swap(child, parent);
        child = parent;
      }
    }

    private void Swap(int child, int parent) {
      var temp = _items[child];
      _items[child] = _items[parent];
      _items[parent] = temp;
    }

    private int Compare(int x, int y) {
      return _comparer.Compare(_items[x], _items[y]);
    }

    private void ExpandArray() {
      if (_count == _items.Length) {
        var array = new T[_count * 2];
        Array.Copy(_items, array, _count);
        _items = array;
      }
    }

    private static int LeftChild(int i) {
      return i * 2 + 1;
    }

    private static int Parent(int i) {
      return (i - 1) / 2;
    }

    private static int RightChildFromLeftChild(int i) {
      return i + 1;
    }
  }
}
