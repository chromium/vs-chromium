// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Collections {
  public partial class SlimDictionary<TKey, TValue> {
    private struct Entry {
      internal readonly TKey Key;
      internal readonly TValue Value;

      public Entry(TKey key, TValue value) {
        Key = key;
        Value = value;
      }
    }
  }
}