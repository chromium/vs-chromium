using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public interface ITreeViewItem {
    string Text { get; }
    ImageSource Image { get; }
    IList<ITreeViewItem> Children { get; }
    ContextMenu ContextMenu { get; }
  }
}
