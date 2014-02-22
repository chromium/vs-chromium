using System;

namespace VsChromiumCore.Processes {
  [Flags]
  public enum CreateProcessOptions {
    RedirectStdio = 1 << 1,
    AttachDebugger = 1 << 2,
    BreakAwayFromJob = 1 << 3,
  }
}