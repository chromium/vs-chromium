// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromiumCore.Collections {
  public class MinHeap<T> : IHeap<T> {
    private readonly MaxHeap<T> _maxpHeap;

    public MinHeap() {
      _maxpHeap = new MaxHeap<T>(ReverseComparer<T>.Default);
    }

    public MinHeap(int capacity) {
      _maxpHeap = new MaxHeap<T>(capacity, ReverseComparer<T>.Default);
    }

    public MinHeap(int capacity, IComparer<T> comparer) {
      _maxpHeap = new MaxHeap<T>(capacity, new ReverseComparer<T>(comparer));
    }

    public MinHeap(IComparer<T> comparer) {
      _maxpHeap = new MaxHeap<T>(new ReverseComparer<T>(comparer));
    }

    public T Min { get { return Root; } }

    public void Clear() {
      _maxpHeap.Clear();
    }

    public void Add(T item) {
      _maxpHeap.Add(item);
    }

    public T Remove() {
      return _maxpHeap.Remove();
    }

    public int Count { get { return _maxpHeap.Count; } }

    public T Root { get { return _maxpHeap.Root; } }
  }
}
