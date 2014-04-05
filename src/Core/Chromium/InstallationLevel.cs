using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.Core.Chromium {
  public enum InstallationLevel {
    System,
    User,
    Developer
  }

  public static class InstallationLevelExtensions {
    public static string LevelString(this InstallationLevel level) {
      if (level == InstallationLevel.System)
        return "System-level";
      else
        return "User-level";
    }
  }
}
