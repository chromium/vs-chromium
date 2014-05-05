using System.ComponentModel.Composition;

namespace VsChromium.Wpf {
  [Export(typeof(IClipboard))]
  public class Clipboard : IClipboard {
    public void SetText(string text) {
      System.Windows.Clipboard.SetText(text);
    }
  }
}