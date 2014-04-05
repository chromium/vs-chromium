using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.FileNames;
using VsChromium.Core.Processes;
using VsChromium.Core.Win32.Processes;

namespace VsChromium.Core.Chromium {
  public class InstallationData {
    public InstallationData() {

    }

    public static InstallationData Create(int pid) {
      Process p = Process.GetProcessById(pid);
      if (p == null)
        return null;

      InstallationEnumerator enumerator = new InstallationEnumerator();
      foreach (InstallationData data in enumerator) {
        FullPathName fullPath = new FullPathName(p.MainModule.FileName);
        if (fullPath.StartsWith(data.InstallationPath))
          return data;
      }
      return new InstallationData(p.MainModule.FileName, InstallationLevel.Developer, 0, "Developer Chrome", String.Empty);
    }

    public InstallationData(string exePath, InstallationLevel level, int iconIndex, string name, string version) {
      string location = Path.GetDirectoryName(exePath);

      Distribution = DistributionType.Canary;
      Architecture = ProcessUtility.GetMachineType(exePath);
      Level = level;
      InstallationPath = new FileNames.FullPathName(location);
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
    public FullPathName InstallationPath { get; set; }
    public InstallationLevel Level { get; set; }
    public MachineType Architecture { get; set; }
    public string Version { get; set; }
    public string Name { get; set; }
    public bool IsDefaultBrowser { get; set; }
    public int IconIndex { get; set; }
  }
}
