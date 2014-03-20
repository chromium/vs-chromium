using System.ComponentModel.Composition;
using VsChromium.Features.ChromiumExplorer;

namespace VsChromium.Features.AutoUpdate {
  [Export(typeof(IUpdateNotificationListener))]
  public class UpdateNotificationListener : IUpdateNotificationListener {
    private readonly IChromiumExplorerToolWindowAccessor _chromiumExplorerToolWindowAccessor;

    [ImportingConstructor]
    public UpdateNotificationListener(IChromiumExplorerToolWindowAccessor chromiumExplorerToolWindowAccessor) {
      _chromiumExplorerToolWindowAccessor = chromiumExplorerToolWindowAccessor;
    }

    public void NotifyUpdate(UpdateInfo updateInfo) {
      var window = _chromiumExplorerToolWindowAccessor.GetToolWindow();
      if (window != null) {
        window.NotifyPackageUpdate(updateInfo);
      }
    }
  }
}