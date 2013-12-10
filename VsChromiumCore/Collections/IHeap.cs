// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromiumCore.Collections {
  public interface IHeap<T> {
    int Count { get; }
    T Root { get; }

    void Clear();

    void Add(T item);
    T Remove();
  }
}
