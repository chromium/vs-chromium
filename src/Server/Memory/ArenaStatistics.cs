// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Core.Memory {
  /// <summary>
  /// Memory of the arenas of an <see cref="ArenaAllocator"/> (see
  /// <see cref="ArenaAllocator.GetStatistics"/>).
  /// </summary>
  public struct ArenaStatistics {
    /// <summary>The number of arenas still allocated.</summary>
    public int ArenaCount;
    /// <summary>The native memory of the arenas.</summary>
    public long ArenaBytes;
    /// <summary>The bytes allocated in the arenas (the rest of the current arena is free).</summary>
    public long UsedBytes;
    /// <summary>The bytes of the slots still in use.</summary>
    public long LiveBytes;
    /// <summary>
    /// The number of full arenas with less than half of their allocated bytes still in use.
    /// </summary>
    public int SparseArenaCount;

    /// <summary>
    /// The bytes allocated but not in use anymore (released with their arena, when none of its
    /// slots is in use anymore).
    /// </summary>
    public long DeadBytes => UsedBytes - LiveBytes;
  }
}
