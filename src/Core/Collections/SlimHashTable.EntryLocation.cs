// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> {
    private struct EntryLocation {
      public EntryLocation(int slotIndex, int entryIndex, int previousEntryIndex) {
        SlotIndex = slotIndex;
        EntryIndex = entryIndex;
        PreviousEntryIndex = previousEntryIndex;
      }

      public bool IsValid {
        get { return SlotIndex >= 0; }
      }
      public int SlotIndex { get; }

      public int EntryIndex { get; }

      public int PreviousEntryIndex { get; }
    }
  }
}