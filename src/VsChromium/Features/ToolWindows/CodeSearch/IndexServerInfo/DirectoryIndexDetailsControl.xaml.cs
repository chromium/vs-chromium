// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace VsChromium.Features.ToolWindows.CodeSearch.IndexServerInfo {
  /// <summary>
  /// Interaction logic for ProjectDetailsControl.xaml
  /// </summary>
  public partial class DirectoryIndexDetailsControl {
    private GridViewColumnHeader _listViewSortCol;
    private SortAdorner _listViewSortAdorner;

    private GridViewColumnHeader _largeFileListViewSortCol;
    private SortAdorner _largeFilesListViewSortAdorner;

    public DirectoryIndexDetailsControl() {
      InitializeComponent();
    }

    private void ListViewColumnHeader_Click(object sender, RoutedEventArgs e) {
      GridViewColumnHeader column = (sender as GridViewColumnHeader);
      string sortBy = column.Tag.ToString();
      if (_listViewSortCol != null) {
        AdornerLayer.GetAdornerLayer(_listViewSortCol).Remove(_listViewSortAdorner);
        ListView.Items.SortDescriptions.Clear();
      }

      ListSortDirection newDir = ListSortDirection.Ascending;
      if (_listViewSortCol == column && _listViewSortAdorner.Direction == newDir)
        newDir = ListSortDirection.Descending;

      _listViewSortCol = column;
      _listViewSortAdorner = new SortAdorner(_listViewSortCol, newDir);
      AdornerLayer.GetAdornerLayer(_listViewSortCol).Add(_listViewSortAdorner);
      ListView.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
    }

    private void LargeFilesListViewColumnHeader_Click(object sender, RoutedEventArgs e) {
      GridViewColumnHeader column = (sender as GridViewColumnHeader);
      string sortBy = column.Tag.ToString();
      if (_largeFileListViewSortCol != null) {
        AdornerLayer.GetAdornerLayer(_largeFileListViewSortCol).Remove(_largeFilesListViewSortAdorner);
        LargeFilesListView.Items.SortDescriptions.Clear();
      }

      ListSortDirection newDir = ListSortDirection.Ascending;
      if (_largeFileListViewSortCol == column && _largeFilesListViewSortAdorner.Direction == newDir)
        newDir = ListSortDirection.Descending;

      _largeFileListViewSortCol = column;
      _largeFilesListViewSortAdorner = new SortAdorner(_largeFileListViewSortCol, newDir);
      AdornerLayer.GetAdornerLayer(_largeFileListViewSortCol).Add(_largeFilesListViewSortAdorner);
      LargeFilesListView.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
    }

    public class SortAdorner : Adorner {
      private static Geometry ascGeometry =
        Geometry.Parse("M 0 4 L 3.5 0 L 7 4 Z");

      private static Geometry descGeometry =
        Geometry.Parse("M 0 0 L 3.5 4 L 7 0 Z");

      public ListSortDirection Direction { get; private set; }

      public SortAdorner(UIElement element, ListSortDirection dir)
        : base(element) {
        this.Direction = dir;
      }

      protected override void OnRender(DrawingContext drawingContext) {
        base.OnRender(drawingContext);

        if (AdornedElement.RenderSize.Width < 20)
          return;

        TranslateTransform transform = new TranslateTransform
        (
          AdornedElement.RenderSize.Width - 15,
          (AdornedElement.RenderSize.Height - 5) / 2
        );
        drawingContext.PushTransform(transform);

        Geometry geometry = ascGeometry;
        if (this.Direction == ListSortDirection.Descending)
          geometry = descGeometry;
        drawingContext.DrawGeometry(Brushes.Black, null, geometry);

        drawingContext.Pop();
      }
    }
  }
}
