using VsChromium.Features.ToolWindows.BuildExplorer;
using VsChromium.Features.ToolWindows.SourceExplorer;

namespace VsChromium.Features.ToolWindows {
  public interface IToolWindowAccessor {
    SourceExplorerToolWindow SourceExplorer { get; }
    BuildExplorerToolWindow BuildExplorer { get; }
  }
}