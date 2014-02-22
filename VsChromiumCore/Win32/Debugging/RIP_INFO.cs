using System.Runtime.InteropServices;

namespace VsChromiumCore.Win32.Debugging {
  [StructLayout(LayoutKind.Sequential)]
  public struct RIP_INFO {
    public uint dwError;
    public uint dwType;
  }
}