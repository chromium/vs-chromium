using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsChromium.Commands;
using VsChromium.Core.Chromium;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public class InstalledBuildViewModel {
    private InstallationData _installationData;
    private List<ChromeProcessViewModel> _processes;
    private ImageSource _icon;

    public InstalledBuildViewModel(InstallationData installationData) {
      _installationData = installationData;
      _processes = new List<ChromeProcessViewModel>();
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

    public IList<ChromeProcessViewModel> Processes {
      get { return _processes; }
    }

    public bool IsRunning {
      get { return _processes.Count > 0; }
    }

    public bool IsNotRunning {
      get { return !IsRunning; }
    }

    public bool IsDebugging {
      get { return false; }
    }

    public bool IsNotDebugging {
      get { return !IsDebugging; }
    }

    public ImageSource IconImage {
      get {
        return _icon;
      }
    }

    public string DisplayText {
      get {
        return String.Format(
            "{0} {1} (v{2}, {3})",
            _installationData.Name,
            _installationData.Architecture.ToString().ToLower(),
            _installationData.Version,
            _installationData.Level.LevelString());
      }
    }
  }
}
