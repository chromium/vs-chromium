using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Chromium;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public class ChromeProcessViewModel {
    public uint Pid { get; set; }
    public InstallationData InstallationData { get; set; }
  }
}
