// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> {
    private struct Locker : IDisposable {
      private readonly SlimHashTable<TKey, TValue> _table;

      public Locker(SlimHashTable<TKey, TValue> table) {
        _table = table;
        _table._locker();
      }

      public void Dispose() {
        _table._unlocker();
      }
    }
  }
}