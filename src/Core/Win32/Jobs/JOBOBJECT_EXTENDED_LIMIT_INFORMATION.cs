using System;
using System.Runtime.InteropServices;

namespace VsChromium.Core.Win32.Jobs {
  [StructLayout(LayoutKind.Sequential)]
  struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
    public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
    public IO_COUNTERS IoInfo;
    public UInt32 ProcessMemoryLimit;
    public UInt32 JobMemoryLimit;
    public UInt32 PeakProcessMemoryUsed;
    public UInt32 PeakJobMemoryUsed;
  }
}