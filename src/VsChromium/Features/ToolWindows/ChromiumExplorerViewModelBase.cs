using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.ComponentModelHost;
using VsChromium.Core.Linq;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows {
  public class ChromiumExplorerViewModelBase : INotifyPropertyChanged {
    private readonly TreeViewRootNodes<TreeViewItemViewModel> _rootNodes = new TreeViewRootNodes<TreeViewItemViewModel>();
    private IComponentModel _componentModel;
    private List<TreeViewItemViewModel> _activeRootNodes;
    private IStandarImageSourceFactory _imageSourceFactory;

    /// <summary>
    /// Databound!
    /// </summary>
    public TreeViewRootNodes<TreeViewItemViewModel> RootNodes { get { return _rootNodes; } }

    public List<TreeViewItemViewModel> ActiveRootNodes { get { return _activeRootNodes; } }
    public IComponentModel ComponentModel { get { return _componentModel; } }
    public IStandarImageSourceFactory ImageSourceFactory { get { return _imageSourceFactory; } }

    public event PropertyChangedEventHandler PropertyChanged;

    public virtual void OnToolWindowCreated(IServiceProvider serviceProvider) {
      _componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
      _imageSourceFactory = _componentModel.DefaultExportProvider.GetExportedValue<IStandarImageSourceFactory>();
    }

    protected void SetRootNodes(List<TreeViewItemViewModel> newRootNodes, string defaultText = "") {
      // Don't update if we are passed in the already active collection.
      if (object.ReferenceEquals(_activeRootNodes, newRootNodes))
        return;
      _activeRootNodes = newRootNodes;

      // Move the active root nodes into the observable collection so that
      // the TreeView is refreshed.
      _rootNodes.Clear();
      if (_activeRootNodes.Count == 0) {
        if (!string.IsNullOrEmpty(defaultText)) {
          _rootNodes.Add(new TextItemViewModel(_imageSourceFactory, null, defaultText));
        }
      } else {
        _activeRootNodes.ForAll(x => _rootNodes.Add(x));
      }
    }

    public static void ExpandNodes(IEnumerable<TreeViewItemViewModel> source, bool expandAll) {
      source.ForAll(x => {
        if (expandAll)
          ExpandAll(x);
        else
          x.IsExpanded = true;
      });
    }

    public static void ExpandAll(TreeViewItemViewModel item) {
      item.IsExpanded = true;
      item.Children.ForAll(ExpandAll);
    }

    protected virtual void OnPropertyChanged(string propertyName) {
      PropertyChangedEventHandler handler = PropertyChanged;
      if (handler != null)
        handler(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
