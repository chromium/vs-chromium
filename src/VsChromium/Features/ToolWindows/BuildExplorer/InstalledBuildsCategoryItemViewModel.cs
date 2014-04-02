using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using VsChromium.Core.Chromium;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  class InstalledBuildsCategoryItemViewModel : TreeViewItemViewModel {
    private readonly IList<TreeViewItemViewModel> _children;

    public InstalledBuildsCategoryItemViewModel(IStandarImageSourceFactory imageSourceFactory)
        : base(imageSourceFactory, null, true) {
      InstallationEnumerator enumerator = new InstallationEnumerator();
      _children = enumerator
          .Select(x => new InstalledBuildItemViewModel(StandarImageSourceFactory, this, x))
          .ToList<TreeViewItemViewModel>();
    }

    public string Text { get { return "Installed Builds"; } }

    public override int ChildrenCount { get { return _children.Count; } }

    public override ImageSource ImageSourcePath { get { return null; } }

    protected override IEnumerable<TreeViewItemViewModel> GetChildren() {
      return _children;
    }
  }
}
