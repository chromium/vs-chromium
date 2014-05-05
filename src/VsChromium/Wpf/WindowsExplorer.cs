using System.ComponentModel.Composition;
using System.Diagnostics;

namespace VsChromium.Wpf {
  [Export(typeof(IWindowsExplorer))]
  public class WindowsExplorer : IWindowsExplorer {
    public void OpenContainingFolder(string path) {
      Process.Start("explorer.exe", "/select," + path);
    }
  }
}