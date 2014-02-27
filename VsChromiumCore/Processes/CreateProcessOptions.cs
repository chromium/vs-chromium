using System;

namespace VsChromiumCore.Processes {
  [Flags]
  public enum CreateProcessOptions {
    Default = 0,
    AttachDebugger = 1 << 2,
    BreakAwayFromJob = 1 << 3,
  }
}