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
using VsChromium.Core.Chromium;
using VsChromium.Core.FileNames;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public class InstalledBuildItemViewModel : TreeViewItemViewModel {
    private InstallationData _installationData;
    private ImageSource _imageSource;

    public InstalledBuildItemViewModel(
        IStandarImageSourceFactory imageSourceFactory, 
        TreeViewItemViewModel parent,
        InstallationData installationData) 
      : base(imageSourceFactory, parent, true) {
      _installationData = installationData;
      IntPtr hicon = IntPtr.Zero;

      try {
        string iconPath = Path.Combine(_installationData.InstallationPath.FullName, "chrome.exe");
        ushort index = (ushort)_installationData.IconIndex;
        hicon = Core.Win32.Shell.NativeMethods.ExtractAssociatedIcon(IntPtr.Zero, iconPath, ref index);
        using (Icon icon = Icon.FromHandle(hicon)) {
          _imageSource = Imaging.CreateBitmapSourceFromHIcon(
              icon.Handle, 
              Int32Rect.Empty, 
              BitmapSizeOptions.FromEmptyOptions());
        }
      } catch {
        _imageSource = null;
      } finally {
        if (hicon != IntPtr.Zero)
          Core.Win32.Shell.NativeMethods.DestroyIcon(hicon);
      }
    }

    public string Text { 
      get { 
        return String.Format(
            "{0} {1} (v{2}, {3})", 
            _installationData.Name, 
            _installationData.Architecture.ToString().ToLower(),
            _installationData.Version, 
            _installationData.Level.LevelString());
      } 
    }

    public override ImageSource ImageSourcePath { get { return _imageSource; } }

    public override int ChildrenCount { get { return 0; } }

    protected override IEnumerable<TreeViewItemViewModel> GetChildren() {
      return new List<TreeViewItemViewModel>();
    }
  }
}
