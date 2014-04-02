using Microsoft.VisualStudio.ComponentModelHost;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  class BuildExplorerViewModel : ChromiumExplorerViewModelBase {
    /// <summary>
    /// Databound!
    /// </summary>
    public bool AutoAttach { get; set; }
  }
}
