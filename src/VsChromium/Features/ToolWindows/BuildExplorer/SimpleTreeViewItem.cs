using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public class SimpleTreeViewItem : BuildExplorerTreeViewItem {
    private string _text;
    private ImageSource _imageSource;
    private List<ITreeViewItem> _children;

    public SimpleTreeViewItem(string text, ImageSource imageSource) {
      _text = text;
      _imageSource = imageSource;
      _children = new List<ITreeViewItem>();
    }

    public override string Text {
      get { return _text; }
    }

    public override ImageSource Image {
      get { return _imageSource; }
    }

    public override IList<ITreeViewItem> Children {
      get { return _children; }
    }

    public override ContextMenu ContextMenu {
      get { return null; }
    }
  }
}
