// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
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
    /// Select the visual TreeViewItem corresponding to the ViewModel |item|
    /// object. Note: This method is synchronous (i.e. does not post any message
    /// to the UI thread queue).
    /// </summary>
    public static TreeViewItem SelectTreeViewItem(TreeView treeView, IHierarchyObject dataObject, bool select = true) {
      // Build child->parent->ancestor(s) sequenc as a stack so we can go
      // top-down in the tree during next phase.
      var dataObjectStack = new Stack<IHierarchyObject>();
      while (dataObject != null) {
        dataObjectStack.Push(dataObject);
        dataObject = dataObject.GetParent();
      }

      // For each visual element in the stack, expand and bring to view the
      // corresponding TreeViewItem
      ItemsControl parentItemsControl = treeView;
      IHierarchyObject parentDataObject = null;
      while (dataObjectStack.Count > 0) {
        var childDataObject = dataObjectStack.Pop();

        if (childDataObject.IsVisual) {
          var childItemsControl = BringDataObjectToView(parentItemsControl, parentDataObject, childDataObject);
          if (childItemsControl == null)
            break;
          parentItemsControl = childItemsControl;
        }

        parentDataObject = childDataObject;
      }

      // If the desired selection is found, select it
      var desiredSelection = parentItemsControl as TreeViewItem;
      if (select) {
        if (desiredSelection != null) {
          //var model = (TreeViewItemViewModel) parentDataObject;
          //if (model != null) {
          //  model.IsSelected = true;
          //}
          SetSelectedItem(treeView, desiredSelection);
        }
      }
      return desiredSelection;
    }

    /// <summary>
    /// Programmatically select an item from a tree view (idea from
    /// http://goo.gl/w0KFL5)
    /// </summary>
    public static void SetSelectedItem(TreeView treeView, TreeViewItem item) {
      Logger.WrapActionInvocation(() => {
        //Logger.Log("Select TV item: before: tv.Select=\"{0}\"-{1}",
        //  treeView.SelectedItem ?? "null",
        //  treeView.SelectedItem == null ? -1 : treeView.SelectedItem.GetHashCode());
        MethodInfo selectMethod =
          typeof (TreeViewItem).GetMethod("Select",
            BindingFlags.NonPublic | BindingFlags.Instance);

        selectMethod.Invoke(item, new object[] {true});
        //item.IsSelected = true;
        //Logger.Log("Select TV item: after: tv.Select=\"{0}\"-{1}",
        //  treeView.SelectedItem ?? "null",
        //  treeView.SelectedItem == null ? -1 : treeView.SelectedItem.GetHashCode());
      });
    }

    private static ItemsControl BringDataObjectToView(
      ItemsControl parentItemsControl,
      IHierarchyObject parentDataObject,
      IHierarchyObject dataObject) {
      Debug.Assert(parentItemsControl != null);
      Debug.Assert(parentDataObject != null);
      Debug.Assert(dataObject != null);

      // Expand the current container
      if (parentItemsControl is TreeViewItem && !((TreeViewItem)parentItemsControl).IsExpanded) {
        parentItemsControl.SetValue(TreeViewItem.IsExpandedProperty, true);
      }

      // Try to generate the ItemsPresenter and the ItemsPanel. by calling
      // ApplyTemplate.  Note that in the virtualizing case even if the item is
      // marked expanded we still need to do this step in order to regenerate
      // the visuals because they may have been virtualized away.
      parentItemsControl.ApplyTemplate();
      var itemsPresenter =
          (ItemsPresenter)parentItemsControl.Template.FindName("ItemsHost", parentItemsControl);
      if (itemsPresenter != null) {
        itemsPresenter.ApplyTemplate();
      } else {
        // The Tree template has not named the ItemsPresenter, 
        // so walk the descendents and find the child.
        itemsPresenter = FindVisualChild<ItemsPresenter>(parentItemsControl);
        if (itemsPresenter == null) {
          parentItemsControl.UpdateLayout();

          itemsPresenter = FindVisualChild<ItemsPresenter>(parentItemsControl);
        }
      }

      if (itemsPresenter == null) {
        Logger.LogError("Can't find items presenter.");
        return null;
      }

      // Access the custom VSP that exposes BringIntoView 
      var virtualizingStackPanel = VisualTreeHelper.GetChild(itemsPresenter, 0) as MyVirtualizingStackPanel;
      if (virtualizingStackPanel == null) {
        Logger.LogError("Can't find virtual stack panel for parentItemsControl.");
        return null;
      }

      var dataObjetIndex = parentDataObject.GetAllChildren().IndexOf(dataObject);
      if (dataObjetIndex >= parentItemsControl.Items.Count) {
        Logger.LogError("TreeView node has fewer parents than its corresponding data object.");
        return null;
      }

      virtualizingStackPanel.BringIntoView(dataObjetIndex);
      var treeViewItem =
        (ItemsControl) parentItemsControl.ItemContainerGenerator.ContainerFromIndex(dataObjetIndex);
      if (treeViewItem.DataContext != dataObject) {
        Logger.LogError("TreeView item data context is not the right data object.");
        return null;
      }
      return treeViewItem;
    }

    private static ItemsControl BringViewItemToViewOld(DispatcherObject dispatcher, ItemsControl parentItemsControl, IHierarchyObject parentViewItem, IHierarchyObject viewItem) {
      Debug.Assert(parentViewItem != null);
      // Access the custom VSP that exposes BringIntoView 
      var itemsHost = FindVisualChild<MyVirtualizingStackPanel>(parentItemsControl);
      if (itemsHost == null) {
        Logger.LogError("Can't find itemsHost for parentItemsControl.");
        return null;
      }

      var viewItemIndex = parentViewItem.GetAllChildren().IndexOf(viewItem);
      if (viewItemIndex >= parentItemsControl.Items.Count) {
        Logger.LogError("Can't find child of itemsHost.");
        return null;
      }

      // Due to virtualization, BringIntoView may not predict the offset correctly the first time. 
      return TryAction(
        dispatcher,
        () => itemsHost.BringIntoView(viewItemIndex),
        () => (ItemsControl)parentItemsControl.ItemContainerGenerator.ContainerFromIndex(viewItemIndex),
        10);
    }

    private static T TryAction<T>(DispatcherObject dispatcher, Action action, Func<T> func, int maxLoops)
      where T : class {
      for (var i = 0; i < maxLoops; i++) {
        action();
        var result = WpfUtilities.Invoke(dispatcher, DispatcherPriority.Background, func);
        if (result == null) {
          Logger.Log("Trying action again.");
        }
        if (result != null)
          return result;
      }
      return null;
    }

    /// <summary>
    /// Recursively search for an item in this subtree.
    /// </summary>
    /// <param name="container">
    /// The parent ItemsControl. This can be a TreeView or a TreeViewItem.
    /// </param>
    /// <param name="item">
    /// The item to search for.
    /// </param>
    /// <returns>
    /// The TreeViewItem that contains the specified item.
    /// </returns>
    public static TreeViewItem GetTreeViewItem(ItemsControl container, object item) {
      if (container != null) {
        if (container.DataContext == item) {
          return container as TreeViewItem;
        }

        // Expand the current container
        if (container is TreeViewItem && !((TreeViewItem)container).IsExpanded) {
          container.SetValue(TreeViewItem.IsExpandedProperty, true);
        }

        // Try to generate the ItemsPresenter and the ItemsPanel.
        // by calling ApplyTemplate.  Note that in the 
        // virtualizing case even if the item is marked 
        // expanded we still need to do this step in order to 
        // regenerate the visuals because they may have been virtualized away.

        container.ApplyTemplate();
        ItemsPresenter itemsPresenter =
            (ItemsPresenter)container.Template.FindName("ItemsHost", container);
        if (itemsPresenter != null) {
          itemsPresenter.ApplyTemplate();
        } else {
          // The Tree template has not named the ItemsPresenter, 
          // so walk the descendents and find the child.
          itemsPresenter = FindVisualChild<ItemsPresenter>(container);
          if (itemsPresenter == null) {
            container.UpdateLayout();

            itemsPresenter = FindVisualChild<ItemsPresenter>(container);
          }
        }

        Panel itemsHostPanel = (Panel)VisualTreeHelper.GetChild(itemsPresenter, 0);


        // Ensure that the generator for this panel has been created.
        UIElementCollection children = itemsHostPanel.Children;

        MyVirtualizingStackPanel virtualizingPanel =
            itemsHostPanel as MyVirtualizingStackPanel;

        for (int i = 0, count = container.Items.Count; i < count; i++) {
          TreeViewItem subContainer;
          if (virtualizingPanel != null) {
            // Bring the item into view so 
            // that the container will be generated.
            virtualizingPanel.BringIntoView(i);

            subContainer =
                (TreeViewItem)container.ItemContainerGenerator.
                ContainerFromIndex(i);
          } else {
            subContainer =
                (TreeViewItem)container.ItemContainerGenerator.
                ContainerFromIndex(i);

            // Bring the item into view to maintain the 
            // same behavior as with a virtualizing panel.
            subContainer.BringIntoView();
          }

          if (subContainer != null) {
            // Search the next level for the object.
            TreeViewItem resultContainer = GetTreeViewItem(subContainer, item);
            if (resultContainer != null) {
              return resultContainer;
            } else {
              // The object is not under this TreeViewItem
              // so collapse it.
              subContainer.IsExpanded = false;
            }
          }
        }
      }

      return null;
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
  }
}
