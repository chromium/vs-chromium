// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.FileSystemContents {
  /// <summary>
  /// Encapsulation over file contents stored in memory, with support for
  /// skipping a fixed numbers of bytes in prefix and suffix.
  /// </summary>
  public struct FileContentsMemory {
    private readonly SafeHeapBlockHandle _block;
    private readonly long _contentsOffset;
    private readonly long _contentsLength;

    public FileContentsMemory(SafeHeapBlockHandle block, long contentsOffset, long contentsLength) {
      if (contentsOffset < 0)
        throw new ArgumentException("Contents offset must be positive", "contentsOffset");
      if (contentsOffset < 0)
        throw new ArgumentException("Contents length must be positive.", "contentsLength");
      if (checked(contentsOffset + contentsLength) >= block.ByteLength)
        throw new ArgumentException("Contents range must be within the bounds of the memory block.", "contentsOffset");
      _block = block;
      _contentsOffset = contentsOffset;
      _contentsLength = contentsLength;
    }

    /// <summary>
    /// Returns the pointer to the usable memory of this block, i.e. the block
    /// offset added to the start pointer.
    /// </summary>
    public IntPtr Pointer { get { return Pointers.AddPtr(_block.Pointer, _contentsOffset); } }
    /// <summary>
    /// Return the number of bytes of the usable memory of this block.
    /// </summary>
    public long ByteLength { get { return _contentsLength; } }
  }
}