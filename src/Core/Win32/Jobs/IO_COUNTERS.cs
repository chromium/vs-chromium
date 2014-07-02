using System;
using System.Runtime.InteropServices;

namespace VsChromium.Core.Win32.Jobs {
  [StructLayout(LayoutKind.Sequential)]
  struct IO_COUNTERS {
    public UInt64 ReadOperationCount;
    public UInt64 WriteOperationCount;
    public UInt64 OtherOperationCount;
    public UInt64 ReadTransferCount;
    public UInt64 WriteTransferCount;
    public UInt64 OtherTransferCount;
  }
}