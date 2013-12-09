// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsChromiumCore.Linq;
using VsChromiumPackage.Views;
using VsChromiumPackage.Wpf;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  /// <summary>
  /// Base class for all ViewModel classes displayed by TreeViewItems.
  /// This acts as an adapter between a raw data object and a TreeViewItem.
  /// </summary>
  public class TreeViewItemViewModel : INotifyPropertyChanged, IHierarchyObject {
    private const int _initialItemCountLimit = 100;
    private static readonly TreeViewItemViewModel _dummyChild = new TreeViewItemViewModel();

    private readonly LazyObservableCollection<TreeViewItemViewModel> _children;
    private readonly ITreeViewItemViewModelHost _host;
    private readonly TreeViewItemViewModel _parentViewModel;
    private bool _isExpanded;
    private bool _isSelected;

    // This is used to create the DummyChild instance.
    private TreeViewItemViewModel() {
    }

    protected TreeViewItemViewModel(
        ITreeViewItemViewModelHost host,
        TreeViewItemViewModel parentViewModel,
        bool lazyLoadChildren) {
      this._host = host;
      this._parentViewModel = parentViewModel;
      this._children = new LazyObservableCollection<TreeViewItemViewModel>(_initialItemCountLimit,
          CreateLazyItemViewModel);
      if (lazyLoadChildren)
        this._children.Add(_dummyChild);
    }

    public IStandarImageSourceFactory StandarImageSourceFactory {
      get {
        return this._host.StandarImageSourceFactory;
      }
    }

    public ITreeViewItemViewModelHost Host {
      get {
        return this._host;
      }
    }

    public virtual int ChildrenCount {
      get {
        return 0;
      }
    }

    public virtual ImageSource ImageSourcePath {
      get {
        return new BitmapImage();
      }
    }

    /// <summary>
    /// Returns the logical child items of this object.
    /// </summary>
    public LazyObservableCollection<TreeViewItemViewModel> Children {
      get {
        // Lazy load the child items, if necessary.
        LoadChildren();
        return this._children;
      }
    }

    /// <summary>
    /// Returns true if this object's Children have not yet been populated.
    /// </summary>
    public bool HasDummyChild {
      get {
        return this._children.Count == 1 && this._children[0] == _dummyChild;
      }
    }

    /// <summary>
    /// Gets/sets whether the TreeViewItem 
    /// associated with this object is expanded.
    /// </summary>
    public bool IsExpanded {
      get {
        return this._isExpanded;
      }
      set {
        // Expand all the way up to the root.
        if (value && this._parentViewModel != null) {
          this._parentViewModel.IsExpanded = true;
        }

        if (value) {
          LoadChildren();
        }

        if (value != this._isExpanded) {
          this._isExpanded = value;
          OnPropertyChanged("IsExpanded");
        }
      }
    }

    /// <summary>
    /// Gets/sets whether the TreeViewItem 
    /// associated with this object is selected.
    /// </summary>
    public bool IsSelected {
      get {
        return this._isSelected;
      }
      set {
        if (value != this._isSelected) {
          this._isSelected = value;
          OnPropertyChanged("IsSelected");
        }
      }
    }

    public TreeViewItemViewModel ParentViewModel {
      get {
        return this._parentViewModel;
      }
    }

    IHierarchyObject IHierarchyObject.Parent {
      get {
        return ParentViewModel;
      }
    }

    IEnumerable<IHierarchyObject> IHierarchyObject.Children {
      get {
        return Children.Cast<IHierarchyObject>();
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private LazyItemViewModel CreateLazyItemViewModel() {
      var result = new LazyItemViewModel(this._host, this);
      if (ChildrenCount != 0)
        result.Text = string.Format("(Click to expand {0:n0} additional items...)",
            ChildrenCount - _initialItemCountLimit);
      result.Selected += () => {
        var node = this._children.ExpandLazyNode();
        node.IsSelected = true;
      };
      return result;
    }

    public void EnsureAllChildrenLoaded() {
      LoadChildren();
      this._children.ExpandLazyNode();
    }

    private void LoadChildren() {
      // Lazy load the child items, if necessary.
      if (HasDummyChild) {
        this._children.Remove(_dummyChild);
        GetChildren().ForAll(x => this._children.Add(x));
      }
    }

    protected virtual IEnumerable<TreeViewItemViewModel> GetChildren() {
      return Enumerable.Empty<TreeViewItemViewModel>();
    }

    protected virtual void OnPropertyChanged(string propertyName) {
      if (PropertyChanged != null)
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
