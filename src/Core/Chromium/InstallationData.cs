using System;
using System.Diagnostics;
using System.IO;
using VsChromium.Core.Files;
using VsChromium.Core.Processes;
using VsChromium.Core.Win32.Processes;

namespace VsChromium.Core.Chromium {
  public class InstallationData {
    public InstallationData() {

    }

    public static InstallationData Create(NtProcess proc) {
      InstallationEnumerator enumerator = new InstallationEnumerator();
      foreach (InstallationData data in enumerator) {
        FullPath fullPath = new FullPath(proc.Win32ProcessImagePath);
        if (fullPath.StartsWith(data.InstallationPath))
          return data;
      }
      return new InstallationData(
          proc.Win32ProcessImagePath, 
          InstallationLevel.Developer, 
          0, 
          "Developer Chrome", 
          String.Empty);
    }

    public InstallationData(string exePath, InstallationLevel level, int iconIndex, string name, string version) {
      string location = Path.GetDirectoryName(exePath);

      Distribution = DistributionType.Canary;
      Architecture = ProcessUtility.GetMachineType(exePath);
      Level = level;
      InstallationPath = new FullPath(location);
      if (InstallationPath.HasComponent("Chrome SxS"))
        Distribution = DistributionType.Canary;
      else if (InstallationPath.HasComponent("Chrome"))
        Distribution = DistributionType.Chrome;
      else
        Distribution = DistributionType.Chromium;

      IconIndex = iconIndex;
      Name = name;
      Version = version;
    }

    public DistributionType Distribution { get; set; }
    public FullPath InstallationPath { get; set; }
    public InstallationLevel Level { get; set; }
    public MachineType Architecture { get; set; }
    public string Version { get; set; }
    public string Name { get; set; }
    public bool IsDefaultBrowser { get; set; }
    public int IconIndex { get; set; }
  }
}
