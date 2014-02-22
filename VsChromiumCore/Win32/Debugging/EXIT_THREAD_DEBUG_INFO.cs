using System.Runtime.InteropServices;

namespace VsChromiumCore.Win32.Debugging {
  [StructLayout(LayoutKind.Sequential)]
  public struct EXIT_THREAD_DEBUG_INFO {
    public uint dwExitCode;
  }
}