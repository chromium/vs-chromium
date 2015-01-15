using System.Windows.Media;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows {
  public class TextErrorItemViewModel : TreeViewItemViewModel {
    private readonly string _text;

    public TextErrorItemViewModel(IStandarImageSourceFactory imageSourceFactory, TreeViewItemViewModel parent, string text)
      : base(imageSourceFactory, parent, false) {
      _text = text;
    }

    public string Text { get { return _text; } }

    public override ImageSource ImageSourcePath { get { return StandarImageSourceFactory.GetImage("Error"); } }
  }
}