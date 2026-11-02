// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.


namespace VsChromium.Core.Collections {
  public partial class SlimHashTable<TKey, TValue> {
    private struct Entry {
      /// <summary>
      /// The hashcode of the entry
      /// </summary>
      internal readonly int HashCode;
      /// <summary>
      /// MinValue: entry is not valid, there is no next free index
      /// &lt;= -1: entry is not valid, next free index is (-value - 1)
      /// 0: entry is not valid
      /// >= 1: entry is valid, next entry index is (value - 1)
      /// MaxValue: entry is valid, but does not point to a next index
      /// </summary>
      private int _nextIndex;
      /// <summary>
      /// The stored value
      /// </summary>
      internal TValue Value;

      internal Entry(TValue value, int hashCode, int nextIndex) {
        Value = value;
        HashCode = hashCode;
        _nextIndex = (nextIndex == -1 ? int.MaxValue : nextIndex + 1);
      }

      public bool IsValid => _nextIndex >= 1;

      public int NextIndex => (_nextIndex >= 1 && _nextIndex < int.MaxValue) ? (_nextIndex - 1) : -1;
      public int NextFreeIndex => (_nextIndex > int.MinValue && _nextIndex < 0) ? (-_nextIndex - 1) : -1;

      public override string ToString() {
        return $"Value={Value} - IsValid={IsValid} = NextIndex={NextIndex} - NextFreeIndex={NextFreeIndex}";
      }

      public void SetNextIndex(int nextIndex) {
        _nextIndex = (nextIndex == -1 ? int.MaxValue : nextIndex + 1);
      }

      public void SetNextFreeIndex(int nextIndex) {
        Value = default(TValue);
        _nextIndex = (nextIndex == -1 ? int.MinValue : -nextIndex - 1);
      }
    }
  }
}