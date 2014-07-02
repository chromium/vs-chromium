using System;
using System.Runtime.InteropServices;

namespace VsChromium.Core.Win32.Jobs {
  [StructLayout(LayoutKind.Sequential)]
  struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
    public Int64 PerProcessUserTimeLimit;
    public Int64 PerJobUserTimeLimit;
    public Int16 LimitFlags;
    public UInt32 MinimumWorkingSetSize;
    public UInt32 MaximumWorkingSetSize;
    public Int16 ActiveProcessLimit;
    public Int64 Affinity;
    public Int16 PriorityClass;
    public Int16 SchedulingClass;
  }
}