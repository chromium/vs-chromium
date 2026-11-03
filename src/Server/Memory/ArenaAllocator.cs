// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace VsChromium.Core.Memory {
  /// <summary>
  /// Allocates the native memory of file contents in arenas: blocks of <see cref="ArenaSize"/>
  /// bytes shared by many files (instead of one block and one finalizable handle per file).
  /// <para>
  /// Thread safe: all the threads allocate from the current arena with an atomic bump pointer;
  /// when it is full, a new arena becomes the current one (once per <see cref="ArenaSize"/> bytes).
  /// Allocations larger than <see cref="LargeAllocationSize"/> get a dedicated block.
  /// </para>
  /// <para>
  /// An arena is released (by the finalizer of its handle) when no slice of it is referenced
  /// anymore: the lifetime of file contents is unchanged (snapshots sharing file contents and
  /// searches running on old snapshots keep them alive). Slots of released file contents are dead
  /// bytes until their arena is released (see <see cref="GetArenas"/>).
  /// </para>
  /// </summary>
  public sealed class ArenaAllocator {
    public const int DefaultArenaSize = 16 * 1024 * 1024;
    public const int DefaultLargeAllocationSize = 512 * 1024;
    /// <summary>
    /// Alignment of the slots (so that vectorized searches start on aligned addresses).
    /// </summary>
    public const int Alignment = 8;

    private readonly object _lock = new object();
    private readonly List<Arena> _arenas = new List<Arena>();
    private Arena _current;

    public ArenaAllocator() : this(DefaultArenaSize, DefaultLargeAllocationSize) {
    }

    public ArenaAllocator(int arenaSize, int largeAllocationSize) {
      if (largeAllocationSize > arenaSize)
        throw new ArgumentException("Large allocations must fit in an arena.", nameof(largeAllocationSize));
      ArenaSize = arenaSize;
      LargeAllocationSize = largeAllocationSize;
    }

    public int ArenaSize { get; }
    public int LargeAllocationSize { get; }

    /// <summary>
    /// Allocates <paramref name="size"/> bytes (not initialized).
    /// </summary>
    public NativeMemorySlice Allocate(int size) {
      if (size < 0)
        throw new ArgumentOutOfRangeException(nameof(size));
      if (size > LargeAllocationSize) {
        return new NativeMemorySlice(NativeHeap.Alloc(size), 0, size, isDedicated: true);
      }

      var alignedSize = GetSlotSize(size);
      while (true) {
        var arena = Volatile.Read(ref _current) ?? NewArena(null);
        // Note: Read the block before reserving the slot: it is cleared when the arena is
        // replaced (after it is full, so that a reservation can't succeed anymore).
        var block = Volatile.Read(ref arena.Block);
        if (block != null) {
          var end = Interlocked.Add(ref arena.Used, alignedSize);
          if (end <= ArenaSize) {
            return new NativeMemorySlice(block, end - alignedSize, size, isDedicated: false);
          }
        }
        // The arena is full (the slot it would have returned is never used: at most one
        // allocation wasted per arena).
        NewArena(arena);
      }
    }

    /// <summary>
    /// Replaces <paramref name="full"/> (null: none yet) with a new current arena, unless another
    /// thread already did.
    /// </summary>
    private Arena NewArena(Arena full) {
      lock (_lock) {
        if (_current != full)
          return _current;
        var arena = new Arena(NativeHeap.Alloc(ArenaSize));
        _arenas.RemoveAll(x => !x.IsAllocated);
        _arenas.Add(arena);
        Volatile.Write(ref _current, arena);
        if (full != null) {
          // From now on, only the file contents (slices) keep the block of the full arena alive.
          Volatile.Write(ref full.Block, null);
        }
        return arena;
      }
    }

    /// <summary>
    /// The arenas still allocated (referenced by file contents, or current): their block and the
    /// number of bytes allocated in it.
    /// </summary>
    public IReadOnlyList<(SafeNativeMemoryHandle Block, int UsedBytes)> GetArenas() {
      var result = new List<(SafeNativeMemoryHandle, int)>();
      lock (_lock) {
        foreach (var arena in _arenas) {
          if (arena.WeakBlock.TryGetTarget(out var block) && !block.IsClosed) {
            result.Add((block, Math.Min(ArenaSize, Volatile.Read(ref arena.Used))));
          }
        }
      }
      return result;
    }

    /// <summary>
    /// The size of the slot of an allocation of <paramref name="size"/> bytes in an arena.
    /// </summary>
    public static int GetSlotSize(int size) {
      return (size + Alignment - 1) & ~(Alignment - 1);
    }

    /// <summary>
    /// The arenas still allocated, and the bytes of their slots still used by
    /// <paramref name="liveSlots"/> (the block and slot size of live allocations: blocks that are
    /// not arenas are ignored). The other allocated bytes are dead: slots of released allocations
    /// (until their arena is released) and the unused end of full arenas.
    /// </summary>
    public ArenaStatistics GetStatistics(IEnumerable<(SafeHandle Block, int SlotSize)> liveSlots) {
      var arenas = GetArenas();
      var liveBytes = new Dictionary<SafeHandle, long>(ReferenceEqualityComparer.Instance);
      foreach (var arena in arenas) {
        liveBytes.Add(arena.Block, 0);
      }
      foreach (var slot in liveSlots) {
        if (slot.Block != null && liveBytes.TryGetValue(slot.Block, out var bytes)) {
          liveBytes[slot.Block] = bytes + slot.SlotSize;
        }
      }

      var result = new ArenaStatistics { ArenaCount = arenas.Count, ArenaBytes = (long)arenas.Count * ArenaSize };
      var current = Volatile.Read(ref _current)?.Block;
      foreach (var arena in arenas) {
        var live = liveBytes[arena.Block];
        result.UsedBytes += arena.UsedBytes;
        result.LiveBytes += live;
        if (arena.Block != current && live * 2 < arena.UsedBytes) {
          result.SparseArenaCount++;
        }
      }
      return result;
    }

    private sealed class Arena {
      /// <summary>
      /// Strong reference while this is the current arena (null afterwards).
      /// </summary>
      public SafeNativeMemoryHandle Block;
      public readonly WeakReference<SafeNativeMemoryHandle> WeakBlock;
      public int Used;

      public Arena(SafeNativeMemoryHandle block) {
        Block = block;
        WeakBlock = new WeakReference<SafeNativeMemoryHandle>(block);
      }

      public bool IsAllocated => WeakBlock.TryGetTarget(out var block) && !block.IsClosed;
    }
  }
}
