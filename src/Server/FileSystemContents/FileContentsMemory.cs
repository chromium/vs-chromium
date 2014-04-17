using System;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Server.Search {
  public class FileContentsMemory {
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

    public long ContentsByteLength { get { return _contentsByteCount; } }
    public IntPtr ContentsPointer { get { return _block.Pointer + _contentsOffset; } }
  }
}