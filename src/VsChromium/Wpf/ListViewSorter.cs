// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace VsChromium.Wpf {
  public class ListViewSorter {
    public static readonly DependencyProperty InitialSortOrderProperty =
      DependencyProperty.RegisterAttached("InitialSortOrder", typeof(ListSortDirection), typeof(ListViewSorter),
        new FrameworkPropertyMetadata(ListSortDirection.Ascending));

    public static readonly DependencyProperty InitialSortColumnProperty =
      DependencyProperty.RegisterAttached("InitialSortColumn", typeof(bool), typeof(ListViewSorter),
        new FrameworkPropertyMetadata(false));

    public static void SetInitialSortOrder(GridViewColumnHeader obj, ListSortDirection value) {
      obj.SetValue(InitialSortOrderProperty, value);

    }

    public static ListSortDirection GetInitialSortOrder(GridViewColumnHeader obj) {
      return (ListSortDirection) obj.GetValue(InitialSortOrderProperty);
    }

    public static void SetInitialSortColumn(GridViewColumnHeader obj, bool value) {
      obj.SetValue(InitialSortColumnProperty, value);

    }

    public static bool GetInitialSortColumn(GridViewColumnHeader obj) {
      return (bool)obj.GetValue(InitialSortColumnProperty);
    }
  }
}