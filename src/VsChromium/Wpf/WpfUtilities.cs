// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
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

    public static void SelectItem(ItemsControl items, IHierarchyObject item) {
      ItemsControl itemsControl = items;
      var viewItems = new Stack<IHierarchyObject>();
      while (item != null) {
        viewItems.Push(item);
        item = item.Parent;
      }

      IHierarchyObject parentViewItem = null;
      while (viewItems.Count > 0) {
        var viewItem = viewItems.Pop();

        itemsControl = BringViewItemToView(items, itemsControl, parentViewItem, viewItem);
        if (itemsControl == null)
          break;

        parentViewItem = viewItem;
      }

      // If the desired selection is found, select it 
      var desiredSelection = itemsControl as TreeViewItem;
      if (desiredSelection != null) {
        desiredSelection.IsSelected = true;
        desiredSelection.Focus();
      }
    }

    private static ItemsControl BringViewItemToView(
      DispatcherObject dispatcher,
      ItemsControl itemsControl,
      IHierarchyObject parentViewItem,
      IHierarchyObject viewItem) {
      // Access the custom VSP that exposes BringIntoView 
      var itemsHost = FindVisualChild<MyVirtualizingStackPanel>(itemsControl);
      if (itemsHost == null) {
        Logger.LogError("Can't find itemsHost for itemsControl.");
        return null;
      }

      var index = (parentViewItem == null ? 0 : parentViewItem.Children.ToList().IndexOf(viewItem));
      if (index >= itemsControl.Items.Count) {
        Logger.LogError("Can't find child of itemsHost.");
        return null;
      }

      // Due to virtualization, BringIntoView may not predict the offset correctly the first time. 
      return TryAction(
        dispatcher,
        () => itemsHost.BringIntoView(index),
        () => (ItemsControl)itemsControl.ItemContainerGenerator.ContainerFromIndex(index),
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
