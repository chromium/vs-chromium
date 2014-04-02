using System.ComponentModel.Composition;
using VsChromium.Features.ToolWindows;

namespace VsChromium.Features.AutoUpdate {
  [Export(typeof(IUpdateNotificationListener))]
  public class UpdateNotificationListener : IUpdateNotificationListener {
    private readonly ToolWindowAccessor _toolWindowAccessor;

    [ImportingConstructor]
    public UpdateNotificationListener(ToolWindowAccessor toolWindowAccessor) {
      _toolWindowAccessor = toolWindowAccessor;
    }

    public void NotifyUpdate(UpdateInfo updateInfo) {
      _toolWindowAccessor.SourceExplorer.NotifyPackageUpdate(updateInfo);
    }
  }
}