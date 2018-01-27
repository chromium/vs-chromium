// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Collections {
  public interface IReadOnlyMap<TKey, TValue> : ICollection<KeyValuePair<TKey, TValue>> {
    TValue this[TKey key] { get; }
    ICollection<TKey> Keys { get; }
    ICollection<TValue> Values { get; }
    object SyncRoot { get; }

    bool TryGetValue(TKey key, out TValue value);
    bool ContainsKey(TKey key);
  }
}
