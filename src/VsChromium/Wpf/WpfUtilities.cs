// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VsChromium.Core.Logging;

namespace VsChromium.Wpf {
  public class DispatchOptions {
    public DispatchOptions() {
      Priority = DispatcherPriority.Normal;
    }

    public Dispatcher Dispatcher { get; set; }
    public DispatcherPriority Priority { get; set; }
    public TimeSpan Delay { get; set; }
    public CancellationToken CancellationToken { get; set; }
  }

  public class DispatchAction : DispatchOptions {
    public Action Action { get; set; }
  }

  public class DispatchFunc<T> : DispatchOptions {
    public Func<T> Func { get; set; }
  }

  public static class WpfUtilities {
    public static T Invoke<T>(DispatcherObject dispatcher, DispatcherPriority priority, Func<T> func) {
      Delegate d = func;
      return (T)dispatcher.Dispatcher.Invoke(priority, d);
    }

    public static T Invoke<T>(DispatchFunc<T> func) {
      Delegate d = func.Func;
      return (T)func.Dispatcher.Invoke(func.Priority, d);
    }

    public static void Invoke(DispatcherObject dispatcher, DispatcherPriority priority, Action action) {
      Delegate d = action;
      dispatcher.Dispatcher.Invoke(priority, d);
    }

    public static void Post(DispatcherObject dispatcher, Action action) {
      var dispatchAction = new DispatchAction {
        Dispatcher = dispatcher.Dispatcher,
        Action = action
      };
      Post(dispatchAction);
    }

    public static void Post(DispatcherObject dispatcher, DispatcherPriority priority, Action action) {
      var dispatchAction = new DispatchAction {
        Dispatcher = dispatcher.Dispatcher,
        Action = action,
        Priority = priority,
      };
      Post(dispatchAction);
    }

    public static void Post(DispatchAction action) {
      MaybeDelayAction(action, () => {
        if (!action.CancellationToken.IsCancellationRequested) {
          Delegate d = action.Action;
          action.Dispatcher.BeginInvoke(DispatcherPriority.Normal, d);
        }
      });
    }

    public static void MaybeDelayAction(DispatchOptions dispatcher, Action action) {
      if (dispatcher.Delay == TimeSpan.Zero) {
        action();
      } else {
        EventHandler callback = (sender, args) => {
          ((DispatcherTimer)sender).Stop();
          action();
        };

        var dt = new DispatcherTimer(dispatcher.Delay, dispatcher.Priority, callback, dispatcher.Dispatcher);
        dt.Start();
      }
    }

    /// <summary>
    /// Select the visual TreeViewItem corresponding to the view model <paramref
    /// name="hierarchyObject"/>.
    /// 
    /// Note: This method is synchronous (i.e. does not post any message to the
    /// UI thread queue) and works only with a virtualizing stack panel.
    /// </summary>
    public static TreeViewItem SelectTreeViewItem(TreeView treeView, IHierarchyObject hierarchyObject) {
      // Build child->parent->ancestor(s) sequence as a stack so we can go
      // top-down in the tree during next phase.
      var objectStack = new Stack<IHierarchyObject>();
      while (hierarchyObject != null) {
        objectStack.Push(hierarchyObject);
        hierarchyObject = hierarchyObject.GetParent();
      }

      // Call "BringObjectToView" on each hierarchy object in the stack, keeping
      // track of the parent "ItemsControl" ("TreeView" or "TreeViewItem") to
      // expand and dive into.
      ItemsControl parentItemsControl = treeView;
      IHierarchyObject parentObject = null;
      while (objectStack.Count > 0) {
        var childObject = objectStack.Pop();
        if (childObject.IsVisual) {
          var childItemsControl = BringObjectToView(parentItemsControl, parentObject, childObject);
          if (childItemsControl == null) {
            Logger.Log("Tree view item corresponding to hierarchy object was not found.");
            parentItemsControl = null;
            break;
          }
          parentItemsControl = childItemsControl;
        }
        parentObject = childObject;
      }

      // If the desired tree view item has been, select it
      var desiredTreeViewItem = parentItemsControl as TreeViewItem;
      if (desiredTreeViewItem != null) {
        SetSelectedItem(treeView, desiredTreeViewItem);
      }
      return desiredTreeViewItem;
    }

    /// <summary>
    /// Programmatically select an item from a tree view (idea from
    /// http://goo.gl/w0KFL5)
    /// </summary>
    public static void SetSelectedItem(TreeView treeView, TreeViewItem item) {
      Logger.WrapActionInvocation(() => {
        MethodInfo selectMethod =
          typeof(TreeViewItem).GetMethod("Select",
            BindingFlags.NonPublic | BindingFlags.Instance);

        selectMethod.Invoke(item, new object[] { true });
      });
    }

    private static ItemsControl BringObjectToView(
      ItemsControl parentItemsControl,
      IHierarchyObject parentDataObject,
      IHierarchyObject dataObject) {
      Debug.Assert(parentItemsControl != null);
      Debug.Assert(parentDataObject != null);
      Debug.Assert(dataObject != null);

      // Expand the the tree view item if necessary.
      var parentTreeViewItem = parentItemsControl as TreeViewItem;
      if (parentTreeViewItem != null) {
        if (!parentTreeViewItem.IsExpanded) {
          // TODO(rpaquay): Use ".IsExpanded = true"?
          parentTreeViewItem.SetValue(TreeViewItem.IsExpandedProperty, true);
        }
      }

      // Try to generate the ItemsPresenter and the ItemsPanel. by calling
      // ApplyTemplate.  Note that in the virtualizing case even if the item is
      // marked expanded we still need to do this step in order to regenerate
      // the visuals because they may have been virtualized away.
      parentItemsControl.ApplyTemplate();
      var itemsPresenter = (ItemsPresenter)parentItemsControl.Template.FindName("ItemsHost", parentItemsControl);
      if (itemsPresenter != null) {
        itemsPresenter.ApplyTemplate();
      } else {
        // The Tree template has not named the ItemsPresenter, 
        // so walk the descendents and find the child.
        itemsPresenter = FindVisualChild<ItemsPresenter>(parentItemsControl);
        if (itemsPresenter == null) {
          parentItemsControl.UpdateLayout();
          itemsPresenter = FindVisualChild<ItemsPresenter>(parentItemsControl);
          if (itemsPresenter == null) {
            Logger.LogError("Can't find items presenter.");
            return null;
          }
        }
      }

      // Access the custom VSP that exposes BringIntoView 
      var virtualizingStackPanel = VisualTreeHelper.GetChild(itemsPresenter, 0) as VirtualizingStackPanel;
      if (virtualizingStackPanel == null) {
        Logger.LogError("Can't find virtual stack panel for parentItemsControl.");
        return null;
      }

      var dataObjetIndex = parentDataObject.GetAllChildren().IndexOf(dataObject);
      if (dataObjetIndex >= parentItemsControl.Items.Count) {
        Logger.LogError("TreeView node has fewer parents than its corresponding data object.");
        return null;
      }

      virtualizingStackPanel.BringIndexIntoViewPublic(dataObjetIndex);
      var treeViewItem = (ItemsControl)parentItemsControl.ItemContainerGenerator.ContainerFromIndex(dataObjetIndex);
      if (treeViewItem.DataContext != dataObject) {
        Logger.LogError("TreeView item data context is not the right data object.");
        return null;
      }
      return treeViewItem;
    }

    /// <summary>
    /// Search for an element of a certain type in the visual tree.
    /// </summary>
    /// <typeparam name="T">The type of element to find.</typeparam>
    /// <param name="visual">The parent element.</param>
    /// <returns>The child element, or null if not found.</returns>
    private static T FindVisualChild<T>(Visual visual) where T : Visual {
      for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++) {
        var child = (Visual)VisualTreeHelper.GetChild(visual, i);
        if (child != null) {
          var correctlyTyped = child as T;
          if (correctlyTyped != null) {
            return correctlyTyped;
          }

          var descendent = FindVisualChild<T>(child);
          if (descendent != null) {
            return descendent;
          }
        }
      }

      return null;
    }

    /// <summary>
    /// Returns the first parent of <paramref name="source"/> of type
    /// <typeparamref name="T"/>.
    /// </summary>
    public static T VisualTreeGetParentOfType<T>(DependencyObject source) where T : DependencyObject {
      while (source != null) {
        if (source is T)
          return (T)source;
        source = VisualTreeHelper.GetParent(source);
      }
      return null;
    }
  }
}
