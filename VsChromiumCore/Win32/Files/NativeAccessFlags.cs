using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VsChromiumCore.Win32.Files {
  [Flags]
  public enum NativeAccessFlags : uint {
    GenericWrite = 0x40000000,
    GenericRead = 0x80000000
  }
}
