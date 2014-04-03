using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  public abstract class BuildExplorerTreeViewItem : ITreeViewItem {
    public abstract string Text { get; }
    public abstract ImageSource Image { get; }
    public abstract IList<ITreeViewItem> Children { get; }
    public abstract ContextMenu ContextMenu { get; }

    public Visibility ImageVisibility {
      get {
        return (Image == null) ? Visibility.Collapsed : Visibility.Visible;
      }
    }
  }
}
