using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using VsChromium.Core.FileNames;
using VsChromium.Core.Processes;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public class InstalledBuildViewModel {
    private InstallationData _installationData;
    private BuildExplorerViewModel _root;
    private List<ChromeProcessViewModel> _processes;
    private ImageSource _icon;

    public InstalledBuildViewModel(BuildExplorerViewModel root, InstallationData installationData) {
      _installationData = installationData;
      _root = root;
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
      }
    }

    public void LoadProcesses() {
      _processes.Clear();
      List<ChromiumProcess> chromes = new List<ChromiumProcess>();
      HashSet<int> chromePids = new HashSet<int>();
      foreach (Process p in Process.GetProcesses()) {
        // System.Diagnostics.Process uses a naive implementation that is unable to deal with many
        // types of processes (such as those already under a debugger, or those with a high
        // privilege level), so use NtProcess instead.
        NtProcess ntproc = new NtProcess(p.Id);
        if (!ntproc.IsValid)
          continue;

        FullPathName processPath = new FullPathName(ntproc.Win32ProcessImagePath);
        if (processPath.StartsWith(_installationData.InstallationPath)) {
          chromes.Add(new ChromiumProcess(p.Id, _installationData));
          chromePids.Add(p.Id);
        }
      }

      foreach (ChromiumProcess chrome in chromes) {
        // Only insert root processes at this level, child processes will be children of one of
        // these processes.
        if (!chromePids.Contains(chrome.ParentPid)) {
          ChromeProcessViewModel viewModel = new ChromeProcessViewModel(_root, chrome);
          viewModel.LoadProcesses(chromes.ToArray());
          _processes.Add(viewModel);
        }
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
