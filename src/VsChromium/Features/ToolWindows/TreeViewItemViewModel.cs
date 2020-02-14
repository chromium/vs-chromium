// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;
using VsChromium.Settings;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows {
  /// <summary>
  /// Base class for all ViewModel classes displayed by TreeViewItems.
  /// This acts as an adapter between a raw data object and a TreeViewItem.
  /// </summary>
  public class TreeViewItemViewModel : INotifyPropertyChanged, IHierarchyObject {
    private static readonly TreeViewItemViewModel DummyChild = new TreeViewItemViewModel();

    private readonly TreeViewItemViewModel _parentViewModel;
    private readonly IStandarImageSourceFactory _imageSourceFactory;
    private readonly LazyObservableCollection<TreeViewItemViewModel> _children;
    private bool _isExpanded;
    private bool _isSelected;
    private Action<TreeViewItemViewModel> _lazeselect;

    /// <summary>
    /// This is used to create the DummyChild instance.
    /// </summary>
    private TreeViewItemViewModel() {
    }

    protected TreeViewItemViewModel(
        IStandarImageSourceFactory imageSourceFactory,
        TreeViewItemViewModel parentViewModel,
        bool lazyLoadChildren) {
      _imageSourceFactory = imageSourceFactory;
      _parentViewModel = parentViewModel;
      _children = new LazyObservableCollection<TreeViewItemViewModel>(HardCodedSettings.MaxExpandedTreeViewItemCount,
                                                                      CreateLazyItemViewModel);
      if (lazyLoadChildren)
        _children.Add(DummyChild);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public TreeViewItemViewModel ParentViewModel {
      get {
        return _parentViewModel;
      }
    }

    public override string ToString() {
      return DisplayText;
    }

    /// <summary>
    /// Databound!
    /// </summary>
    public virtual string DisplayText {
      get { return GetType().FullName; }
    }

    public virtual int ChildrenCount { get { return 0; } }

    /// <summary>
    /// Databound!
    /// </summary>
    public virtual ImageSource ImageSourcePath { get { return null; } }

    /// <summary>
    /// Returns the logical child items of this object.
    /// Databound!
    /// </summary>
    public LazyObservableCollection<TreeViewItemViewModel> Children {
      get {
        // Lazy load the child items, if necessary.
        LoadChildren();
        return _children;
      }
    }

    /// <summary>
    /// Gets/sets whether the TreeViewItem associated with this object is
    /// expanded.
    /// Databound!
    /// </summary>
    public bool IsExpanded {
      get { return _isExpanded; }
      set {
        // Expand all the way up to the root.
        if (value && _parentViewModel != null) {
          _parentViewModel.IsExpanded = true;
        }

        if (value) {
          LoadChildren();
        }

        if (value != _isExpanded) {
          _isExpanded = value;
          OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.IsExpanded));
        }
      }
    }

    /// <summary>
    /// Gets/sets whether the TreeViewItem associated with this object is
    /// selected.
    /// Databound!
    /// </summary>
    public virtual bool IsSelected {
      get { return _isSelected; }
      set {
        if (value != _isSelected) {
          _isSelected = value;
          // Don't notify of a change because it interferes with the tree view
          // programmatic selection, making selection behave erratically.
          //OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.IsSelected));
        }
      }
    }

    public void EnsureAllChildrenLoaded() {
      LoadChildren();
      _children.ExpandLazyNode();
    }

    public static void ExpandNodes(IEnumerable<TreeViewItemViewModel> source, bool expandAll) {
      source.ForAll(x => {
        if (expandAll)
          ExpandAll(x);
        else
          x.IsExpanded = true;
      });
    }

    public Action<TreeViewItemViewModel> LazySelect
    {   get { if (_lazeselect == null) return (TreeViewItemViewModel x) => { }; else return _lazeselect; }
        set { _lazeselect = value; }
    }

    public static void ExpandAll(TreeViewItemViewModel item) {
      item.IsExpanded = true;
      item.Children.ForAll(ExpandAll);
    }

    protected IStandarImageSourceFactory StandarImageSourceFactory {
      get {
        return _imageSourceFactory;
      }
    }

    protected virtual bool IsVisual {
      get {
        return true;
      }
    }

    protected virtual IEnumerable<TreeViewItemViewModel> GetChildren() {
      return Enumerable.Empty<TreeViewItemViewModel>();
    }

    protected virtual void OnPropertyChanged(string propertyName) {
      if (PropertyChanged != null)
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Returns true if this object's Children have not yet been populated.
    /// </summary>
    private bool HasDummyChild {
      get {
        return _children.Count == 1 && _children[0] == DummyChild;
      }
    }

    #region IHierarchyObject

    bool IHierarchyObject.IsVisual {
      get { return this.IsVisual; }
    }

    IHierarchyObject IHierarchyObject.GetParent() {
      return this.ParentViewModel;
    }

    IList<IHierarchyObject> IHierarchyObject.GetAllChildren() {
      this.EnsureAllChildrenLoaded();
      return this.Children.Cast<IHierarchyObject>().ToList();
    }

    #endregion

    private LazyItemViewModel CreateLazyItemViewModel() {
      var result = new LazyItemViewModel(_imageSourceFactory, this);
      if (ChildrenCount != 0)
        result.Text = string.Format("(Click to expand {0:n0} additional items...)",
                                    ChildrenCount - HardCodedSettings.MaxExpandedTreeViewItemCount);
      result.Selected += () => {
          var node = _children.ExpandLazyNode();
          node.IsSelected = true;
          _children.ForAll(x => LazySelect(x));
      };
      return result;
    }

    private void LoadChildren() {
      // Lazy load the child items, if necessary.
      if (HasDummyChild) {
        _children.Remove(DummyChild);
        GetChildren().ForAll(x => _children.Add(x));
      }
    }
  }
}
