using System.ComponentModel.Composition;
using VsChromium.Features.ToolWindows;

namespace VsChromium.Features.AutoUpdate {
  [Export(typeof(IUpdateNotificationListener))]
  public class UpdateNotificationListener : IUpdateNotificationListener {
    private readonly IToolWindowAccessor _toolWindowAccessor;

    [ImportingConstructor]
    public UpdateNotificationListener(IToolWindowAccessor toolWindowAccessor) {
      _toolWindowAccessor = toolWindowAccessor;
    }

    public void NotifyUpdate(UpdateInfo updateInfo) {
      _toolWindowAccessor.CodeSearch.NotifyPackageUpdate(updateInfo);
    }
  }
}