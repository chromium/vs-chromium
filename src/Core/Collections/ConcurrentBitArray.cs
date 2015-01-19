// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Collections {
  public class ConcurrentBitArray : IBitArray {
    private const int BitsPerItem = 64;
    private long[] _bits;
    private int _count;
    private readonly object _lock = new object();
    private static readonly long[] EmptyBits = new long[0];

    public ConcurrentBitArray() {
      _bits = EmptyBits;
    }

    public ConcurrentBitArray(int capacity) {
      _bits = ResizeBits(EmptyBits, capacity);
    }

    private static long[] ResizeBits(long[] bits, int capacity) {
      var slots = (capacity + BitsPerItem - 1) / BitsPerItem;
      var delta =slots - bits.Length;
      if (delta <= 0)
        return bits;

      var newBits = new long[slots];
      Array.Copy(bits, 0, newBits, 0, bits.Length);
      return newBits;
    }

    public int Count { get { return _count; } }

    public void Set(int index, bool value) {
      int slot = index / BitsPerItem;
      long mask = 1L << (index % BitsPerItem);
      lock (_lock) {
        if (slot >= _bits.Length) {
          _bits = ResizeBits(_bits, index);
        }
        bool isSet = (_bits[slot] & mask) != 0;
        if (value && !isSet) {
          _bits[slot] |= mask;
          _count++;
        } else if (!value && isSet) {
          _bits[slot] &= ~mask;
          _count--;
        }
      }
    }

    public bool Get(int index) {
      int slot = index / BitsPerItem;
      long mask = 1L << (index % BitsPerItem);
      lock (_lock) {
        if (slot >= _bits.Length)
          return false;
        return (_bits[slot] & mask) != 0;
      }
    }
  }
}