// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Logging;

namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> {
    private struct Entry {
      public readonly TValue Value;
      /// <summary>
      /// 0 = Invalid entry (<code>null</code>)
      /// -1 = No overflow index
      /// [1.. n] == overflow index [0..n -1]
      /// </summary>
      private readonly int _overflowIndex; // index + 1, so that 0 means "invalid"

      internal Entry(TValue value, int overflowIndex) {
        Value = value;
        _overflowIndex = overflowIndex == -1 ? -1 : overflowIndex + 1;
      }

      /// <summary>
      /// Index into overflow buffer. <code>-1</code> if last entry in the chain.
      /// </summary>
      public int OverflowIndex {
        get {
          Invariants.Assert(IsValid, "Overflow index is not valid");
          return _overflowIndex == -1 ? -1 : _overflowIndex - 1;
        }
      }

      public bool IsValid => _overflowIndex != 0;

      public override string ToString() {
        return $"Value={Value} - OverflowIndex={(IsValid ? OverflowIndex.ToString() : "n/a")}";
      }
    }
  }
}