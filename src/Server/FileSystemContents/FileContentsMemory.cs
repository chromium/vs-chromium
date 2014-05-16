using System;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Server.FileSystemContents {
  public struct FileContentsMemory {
    private readonly SafeHeapBlockHandle _block;
    private readonly int _contentsOffset;
    private readonly long _contentsByteCount;

    public FileContentsMemory(SafeHeapBlockHandle block, int contentsOffset, long contentsByteCount) {
      if (contentsOffset > block.ByteLength)
        throw new ArgumentException("Content offset is too far in buffer", "contentsOffset");
      _block = block;
      _contentsOffset = contentsOffset;
      _contentsByteCount = contentsByteCount;
    }

    /// <summary>
    /// Returns the pointer to the usable memory of this block, i.e. the block
    /// offset added to the start pointer.
    /// </summary>
    public IntPtr ContentsPointer { get { return _block.Pointer + _contentsOffset; } }
    /// <summary>
    /// Return the number of bytes of the usable memory of this block.
    /// </summary>
    public long ContentsByteLength { get { return _contentsByteCount; } }
  }
}