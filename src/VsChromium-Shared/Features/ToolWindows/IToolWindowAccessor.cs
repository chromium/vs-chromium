using System;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Features.ToolWindows.BuildExplorer;
using VsChromium.Features.ToolWindows.CodeSearch;

namespace VsChromium.Features.ToolWindows {
  public interface IToolWindowAccessor {
    CodeSearchToolWindow CodeSearch { get; }
    BuildExplorerToolWindow BuildExplorer { get; }

    IVsWindowFrame FindToolWindow(Guid guid);
  }
}