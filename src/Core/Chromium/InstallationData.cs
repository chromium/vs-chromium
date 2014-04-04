using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.FileNames;
using VsChromium.Core.Win32.Processes;

namespace VsChromium.Core.Chromium {
  public class InstallationData {
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
