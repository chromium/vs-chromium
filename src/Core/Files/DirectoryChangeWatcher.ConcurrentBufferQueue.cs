// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    /// <summary>
    /// A concurrent queue that only supports two atomic operations: adding one elemnet at a time
    /// and removing all elements.
    /// </summary>
    private class ConcurrentBufferQueue<T> {
      private static readonly IList<T> EmptyList = new T[0];
      private List<T> _items = new List<T>();
      private readonly object _lock = new object();

      public void Enqueue(T item) {
        lock (_lock) {
          _items.Add(item);
        }
      }

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

      public void Clear() {
        lock (_lock) {
          _items.Clear();
        }
      }
    }
  }
}