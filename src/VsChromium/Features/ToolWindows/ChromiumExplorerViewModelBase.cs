using Microsoft.VisualStudio.ComponentModelHost;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Linq;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows {
  public class ChromiumExplorerViewModelBase : INotifyPropertyChanged {
    private readonly TreeViewRootNodes<TreeViewItemViewModel> _rootNodes
        = new TreeViewRootNodes<TreeViewItemViewModel>();
    private IComponentModel _componentModel;
    private IList<TreeViewItemViewModel> _currentRootNodesViewModel;
    private IStandarImageSourceFactory _imageSourceFactory;

    /// <summary>
    /// Databound!
    /// </summary>
    public TreeViewRootNodes<TreeViewItemViewModel> RootNodes { get { return _rootNodes; } }

    public IList<TreeViewItemViewModel> CurrentRootNodesViewModel { get { return _currentRootNodesViewModel; } }
    public IComponentModel ComponentModel { get { return _componentModel; } }
    public IStandarImageSourceFactory ImageSourceFactory { get { return _imageSourceFactory; } }

    public event PropertyChangedEventHandler PropertyChanged;

    public virtual void OnToolWindowCreated(IServiceProvider serviceProvider) {
      _componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
      _imageSourceFactory = _componentModel.DefaultExportProvider.GetExportedValue<IStandarImageSourceFactory>();
    }


    protected void SetRootNodes(IEnumerable<TreeViewItemViewModel> source, string defaultText = "") {
      _currentRootNodesViewModel = source.ToList();
      if (_currentRootNodesViewModel.Count == 0 && !string.IsNullOrEmpty(defaultText)) {
        _currentRootNodesViewModel.Add(new TextItemViewModel(_imageSourceFactory, null, defaultText));
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
