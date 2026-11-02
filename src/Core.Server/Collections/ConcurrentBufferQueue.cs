// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  /// <summary>
  /// A concurrent queue that only supports two atomic operations: adding one elemnet at a time
  /// and removing all elements.
  /// </summary>
  public class ConcurrentBufferQueue<T> {
    private static readonly IList<T> EmptyList = new T[0];
    private List<T> _items = new List<T>();
    private readonly object _lock = new object();

    public void Enqueue(T item) {
      lock (_lock) {
        _items.Add(item);
      }
    }

    /// <summary>
    /// Get the contents of the queue, clearing the queue. This is an atomic operation
    /// with a runtime complexity of O(1), as there is no copy involved.
    /// </summary>
    /// <returns></returns>
    public IList<T> DequeueAll() {
      List<T> temp;
      lock (_lock) {
        if (_items.Count == 0) {
          return EmptyList;
        }
        temp = _items;
        _items = new List<T>();
      }
      return temp;
    }

    /// <summary>
    /// Get a copy of contents of the queue. This should only be used for debugging purposes,
    /// as this operation can be slow for large queues, i.e. runtime and memory complexity is O(n).
    /// </summary>
    /// <returns></returns>
    public IList<T> GetCopy() {
      lock (_lock) {
        if (_items.Count == 0) {
          return EmptyList;
        }
        var result = new List<T>(_items.Count);
        result.AddRange(_items);
        return result;
      }
    }

    public void Clear() {
      lock (_lock) {
        _items.Clear();
      }
    }
  }
}