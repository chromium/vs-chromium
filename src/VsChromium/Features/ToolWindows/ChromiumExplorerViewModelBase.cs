using Microsoft.VisualStudio.ComponentModelHost;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Linq;

namespace VsChromium.Features.ToolWindows {
  public class ChromiumExplorerViewModelBase : INotifyPropertyChanged {
    protected readonly TreeViewRootNodes _rootNodes = new TreeViewRootNodes();
    protected IComponentModel _componentModel;
    protected IList<TreeViewItemViewModel> _currentRootNodesViewModel;
    protected ITreeViewItemViewModelHost _host;

    /// <summary>
    /// Databound!
    /// </summary>
    public TreeViewRootNodes RootNodes { get { return _rootNodes; } }

    public event PropertyChangedEventHandler PropertyChanged;

    public virtual void OnToolWindowCreated(IServiceProvider serviceProvider) {
      _componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
    }


    protected void SetRootNodes(IEnumerable<TreeViewItemViewModel> source, string defaultText = "") {
      _currentRootNodesViewModel = source.ToList();
      if (_currentRootNodesViewModel.Count == 0 && !string.IsNullOrEmpty(defaultText)) {
        _currentRootNodesViewModel.Add(new TextItemViewModel(_host, null, defaultText));
      }
      _rootNodes.Clear();
      _currentRootNodesViewModel.ForAll(x => _rootNodes.Add(x));
    }

    protected void ExpandNodes(IEnumerable<TreeViewItemViewModel> source, bool expandAll) {
      source.ForAll(x => {
        if (expandAll)
          ExpandAll(x);
        else
          x.IsExpanded = true;
      });
    }

    protected void ExpandAll(TreeViewItemViewModel item) {
      item.IsExpanded = true;
      item.Children.ForAll(x => ExpandAll(x));
    }

    protected virtual void OnPropertyChanged(string propertyName) {
      PropertyChangedEventHandler handler = PropertyChanged;
      if (handler != null)
        handler(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
