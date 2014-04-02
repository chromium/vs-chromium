using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  class DeveloperBuildsCategoryItemViewModel : TreeViewItemViewModel {
    public DeveloperBuildsCategoryItemViewModel(IStandarImageSourceFactory imageSourceFactory)
        : base(imageSourceFactory, null, false) {
    }

    public string Text { get { return "Developer Builds"; } }

    public override ImageSource ImageSourcePath { 
      get {
        return null;
      } 
    }
  }
}
