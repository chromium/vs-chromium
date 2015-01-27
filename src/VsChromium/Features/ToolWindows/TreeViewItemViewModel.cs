// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Views;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows {
  /// <summary>
  /// Base class for all ViewModel classes displayed by TreeViewItems.
  /// This acts as an adapter between a raw data object and a TreeViewItem.
  /// </summary>
  public class TreeViewItemViewModel : INotifyPropertyChanged, IHierarchyObject {
    private const int _initialItemCountLimit = 100;
    private static readonly TreeViewItemViewModel _dummyChild = new TreeViewItemViewModel();

    private readonly LazyObservableCollection<TreeViewItemViewModel> _children;
    private readonly IStandarImageSourceFactory _imageSourceFactory;
    private readonly TreeViewItemViewModel _parentViewModel;
    private bool _isExpanded;
    private bool _isSelected;

    // This is used to create the DummyChild instance.
    private TreeViewItemViewModel() {
    }

    protected TreeViewItemViewModel(
        IStandarImageSourceFactory imageSourceFactory,
        TreeViewItemViewModel parentViewModel,
        bool lazyLoadChildren) {
      _imageSourceFactory = imageSourceFactory;
      _parentViewModel = parentViewModel;
      _children = new LazyObservableCollection<TreeViewItemViewModel>(_initialItemCountLimit,
                                                                      CreateLazyItemViewModel);
      if (lazyLoadChildren)
        _children.Add(_dummyChild);
    }

    public IStandarImageSourceFactory StandarImageSourceFactory { get { return _imageSourceFactory; } }

    public override string ToString() {
      return DisplayText;
    }

    public virtual string DisplayText {
      get { return GetType().FullName; }
    }

    public virtual int ChildrenCount { get { return 0; } }

    public virtual ImageSource ImageSourcePath { get { return null; } }

    public virtual Visibility ImageVisibility {
      get {
        return (ImageSourcePath == null) ? Visibility.Collapsed : Visibility.Visible;
      } 
    }

    /// <summary>
    /// Returns the logical child items of this object.
    /// </summary>
    public LazyObservableCollection<TreeViewItemViewModel> Children {
      get {
        // Lazy load the child items, if necessary.
        LoadChildren();
        return _children;
      }
    }

    /// <summary>
    /// Returns true if this object's Children have not yet been populated.
    /// </summary>
    public bool HasDummyChild { get { return _children.Count == 1 && _children[0] == _dummyChild; } }

    /// <summary>
    /// Gets/sets whether the TreeViewItem associated with this object is
    /// expanded.
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

    public TreeViewItemViewModel ParentViewModel { get { return _parentViewModel; } }
    protected virtual bool IsVisual { get { return true; } }

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

    public event PropertyChangedEventHandler PropertyChanged;

    private LazyItemViewModel CreateLazyItemViewModel() {
      var result = new LazyItemViewModel(_imageSourceFactory, this);
      if (ChildrenCount != 0)
        result.Text = string.Format("(Click to expand {0:n0} additional items...)",
                                    ChildrenCount - _initialItemCountLimit);
      result.Selected += () => {
        var node = _children.ExpandLazyNode();
        node.IsSelected = true;
      };
      return result;
    }

    public void EnsureAllChildrenLoaded() {
      LoadChildren();
      _children.ExpandLazyNode();
    }

    private void LoadChildren() {
      // Lazy load the child items, if necessary.
      if (HasDummyChild) {
        _children.Remove(_dummyChild);
        GetChildren().ForAll(x => _children.Add(x));
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
