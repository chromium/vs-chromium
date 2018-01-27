// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> {
    private struct EntryLocation {
      private readonly int _index;
      private readonly int _previousOverflowIndex;
      private readonly int _overflowIndex;

      public EntryLocation(int index, int previousOverflowIndex, int overflowIndex) {
        _index = index;
        _previousOverflowIndex = previousOverflowIndex;
        _overflowIndex = overflowIndex;
      }

      public int Index {
        get { return _index; }
      }

      public int PreviousOverflowIndex {
        get { return _previousOverflowIndex; }
      }

      public int OverflowIndex {
        get { return _overflowIndex; }
      }
    }
  }
}