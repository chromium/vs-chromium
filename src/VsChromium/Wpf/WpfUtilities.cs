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
    /// Select the visual TreeViewItem corresponding to the ViewModel |item|
    /// object. Note: This method is synchronous (i.e. does not post any message
    /// to the UI thread queue).
    /// </summary>
    public static void SelectItem(TreeView treeView, IHierarchyObject item) {
      // Build child->parent->ancestor(s) stack so we can go top-down in the tree later on.
      var viewItems = new Stack<IHierarchyObject>();
      while (item != null) {
        viewItems.Push(item);
        item = item.GetParent();
      }

      // For each visual element in the stack, expand and bring to view the
      // corresponding TreeViewItem
      ItemsControl parentItemsControl = treeView;
      IHierarchyObject parentViewItem = null;
      while (viewItems.Count > 0) {
        var viewItem = viewItems.Pop();

        if (viewItem.IsVisual) {
          parentItemsControl = BringViewItemToView(treeView, parentItemsControl, parentViewItem, viewItem);
          if (parentItemsControl == null)
            break;
        }

        parentViewItem = viewItem;
      }

      // If the desired selection is found, select it 
      var desiredSelection = parentItemsControl as TreeViewItem;
      if (desiredSelection != null) {
        SetSelectedItem(treeView, desiredSelection);
        //desiredSelection.IsSelected = true;
        //desiredSelection.Focus();
      }
    }
    /// <summary>
    /// Programmatically select an item from a tree view.
    /// From http://www.askernest.com/archive/2008/01/23/how-to-programmatically-change-the-selecteditem-in-a-wpf-treeview.aspx
    /// </summary>
    public static void SetSelectedItem(TreeView treeView, TreeViewItem item) {
      Logger.WrapActionInvocation(() => {
        //DependencyObject dObject = treeView
        //.ItemContainerGenerator
        //.ContainerFromItem(item);

        //uncomment the following line if UI updates are unnecessary
        //((TreeViewItem)dObject).IsSelected = true;                

        MethodInfo selectMethod =
          typeof (TreeViewItem).GetMethod("Select",
            BindingFlags.NonPublic | BindingFlags.Instance);

        selectMethod.Invoke(item, new object[] {true});
      });
    }

    private static ItemsControl BringViewItemToView(DispatcherObject dispatcher, ItemsControl parentItemsControl, IHierarchyObject parentViewItem, IHierarchyObject viewItem) {
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
        if (result != null)
          return result;
      }
      return null;
    }

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
