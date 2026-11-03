// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Memory {
  /// <summary>
  /// A range of native memory in a <see cref="SafeNativeMemoryHandle"/> block: a whole dedicated
  /// block, or a slot of an arena shared by many slices (see <see cref="ArenaAllocator"/>). The
  /// block stays allocated as long as it is referenced (e.g. by a slice).
  /// </summary>
  public readonly struct NativeMemorySlice {
    public NativeMemorySlice(SafeNativeMemoryHandle block, int offset, int length, bool isDedicated) {
      if (offset < 0 || length < 0 || (long)offset + length > block.ByteLength)
        throw new ArgumentOutOfRangeException(nameof(length), "Slice must be within the bounds of the memory block.");
      Block = block;
      Offset = offset;
      Length = length;
      IsDedicated = isDedicated;
    }

    public SafeNativeMemoryHandle Block { get; }
    public int Offset { get; }
    public int Length { get; }

    /// <summary>
    /// True if <see cref="Block"/> is used only by this slice (see <see cref="Release"/>).
    /// </summary>
    public bool IsDedicated { get; }

    public bool IsNull => Block == null;

    public IntPtr Pointer => Block.Pointer + Offset;

    /// <summary>
    /// The memory of this slice. Note: The span doesn't keep <see cref="Block"/> alive: the caller
    /// must (e.g. <see cref="GC.KeepAlive"/>).
    /// </summary>
    public unsafe Span<byte> Span => new Span<byte>(Pointer.ToPointer(), Length);

    /// <summary>
    /// Releases the memory of this slice now, if possible (a dedicated block). The memory of a slot
    /// of an arena is released with the arena, when no slice of the arena is referenced anymore.
    /// </summary>
    public void Release() {
      if (IsDedicated) {
        Block?.Dispose();
      }
    }
  }
}
