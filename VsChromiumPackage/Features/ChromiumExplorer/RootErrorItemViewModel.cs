using System.Windows.Media;

namespace VsChromiumPackage.Features.ChromiumExplorer {
  public class RootErrorItemViewModel : TreeViewItemViewModel {
    private readonly string _text;

    public RootErrorItemViewModel(ITreeViewItemViewModelHost host, TreeViewItemViewModel parent, string text)
      : base(host, parent, false) {
      _text = text;
    }

    public string Text { get { return _text; } }

    public override ImageSource ImageSourcePath { get { return StandarImageSourceFactory.GetImage("TextEntry"); } }
  }
}