using System.Windows.Media;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows {
  public class TextWarningItemViewModel : TreeViewItemViewModel {
    private readonly string _text;

    public TextWarningItemViewModel(IStandarImageSourceFactory imageSourceFactory, TreeViewItemViewModel parent, string text)
      : base(imageSourceFactory, parent, false) {
      _text = text;
    }

    public string Text { get { return _text; } }

    public override ImageSource ImageSourcePath { get { return StandarImageSourceFactory.GetImage("Warning"); } }
  }
}