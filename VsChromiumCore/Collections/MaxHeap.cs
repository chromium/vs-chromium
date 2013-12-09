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
      this._items = new T[capacity];
      this._count = 0;
      this._comparer = (comparer ?? Comparer<T>.Default);
    }

    public MaxHeap(IComparer<T> comparer)
        : this(_defaultCapacity, comparer) {
    }

    public T Max {
      get {
        return Root;
      }
    }

    public int Count {
      get {
        return this._count;
      }
    }

    public T Root {
      get {
        if (this._count == 0)
          throw new InvalidOperationException("Heap is empty.");

        return this._items[0];
      }
    }

    public T Remove() {
      if (this._count == 0)
        throw new InvalidOperationException("Heap is empty.");

      var result = Root;
      var last = this._count - 1;
      Swap(0, last);
      this._items[this._count - 1] = default(T);
      this._count--;
      SiftDown(0);
      return result;
    }

    public void Add(T value) {
      ExpandArray();
      var leaf = this._count;
      this._items[leaf] = value;
      this._count++;
      SiftUp(leaf);
    }

    public void Clear() {
      Array.Clear(this._items, 0, this._items.Length);
      this._count = 0;
    }

    private void SiftDown(int root) {
      var child = LeftChild(root);
      while (child < this._count) {
        var rightChild = RightChildFromLeftChild(child);
        if (rightChild < this._count && Compare(child, rightChild) < 0)
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
      var temp = this._items[child];
      this._items[child] = this._items[parent];
      this._items[parent] = temp;
    }

    private int Compare(int x, int y) {
      return this._comparer.Compare(this._items[x], this._items[y]);
    }

    private void ExpandArray() {
      if (this._count == this._items.Length) {
        var array = new T[this._count * 2];
        Array.Copy(this._items, array, this._count);
        this._items = array;
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
