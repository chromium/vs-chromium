using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsChromium.Core.Chromium;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public class InstallationTreeViewItem : BuildExplorerTreeViewItem {
    private InstallationData _installationData;
    private ImageSource _icon;

    public InstallationTreeViewItem(InstallationData installationData) {
      _installationData = installationData;

      IntPtr hicon = IntPtr.Zero;

      try {
        string iconPath = Path.Combine(_installationData.InstallationPath.FullName, "chrome.exe");
        ushort index = (ushort)_installationData.IconIndex;
        hicon = Core.Win32.Shell.NativeMethods.ExtractAssociatedIcon(IntPtr.Zero, iconPath, ref index);
        using (Icon icon = Icon.FromHandle(hicon)) {
          _icon = Imaging.CreateBitmapSourceFromHIcon(
              icon.Handle,
              Int32Rect.Empty,
              BitmapSizeOptions.FromEmptyOptions());
        }
      } catch {
        _icon = null;
      } finally {
        if (hicon != IntPtr.Zero)
          Core.Win32.Shell.NativeMethods.DestroyIcon(hicon);
      }
    }

    public override string Text {
      get {
        return String.Format(
            "{0} {1} (v{2}, {3})",
            _installationData.Name,
            _installationData.Architecture.ToString().ToLower(),
            _installationData.Version,
            _installationData.Level.LevelString());
      }
    }

    public override ImageSource Image {
      get { return _icon; }
    }

    public override IList<ITreeViewItem> Children {
      get { return null; }
    }

    public override ContextMenu ContextMenu {
      get { return null; }
    }
  }
}
