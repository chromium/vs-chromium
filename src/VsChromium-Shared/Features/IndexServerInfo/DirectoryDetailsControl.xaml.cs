// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace VsChromium.Features.IndexServerInfo {
  public partial class DirectoryDetailsControl {
    private readonly ListViewSorter _filesByExtensionListViewSorter;
    private readonly ListViewSorter _largeFilesListViewSorter;
    private readonly ListViewSorter _binaryFilesByExtensionListViewSorter;
    private readonly ListViewSorter _largeBinaryFilesListViewSorter;

    public DirectoryDetailsControl() {
      InitializeComponent();
      _filesByExtensionListViewSorter = new ListViewSorter(FilesByExtensionListView);
      _largeFilesListViewSorter = new ListViewSorter(LargeFilesListView);
      _binaryFilesByExtensionListViewSorter = new ListViewSorter(BinaryFilesByExtensionListView);
      _largeBinaryFilesListViewSorter = new ListViewSorter(LargeBinaryFilesListView);
    }

    private void FilesByExtension_ListView_ColumnHeader_Click(object sender, RoutedEventArgs e) {
      _filesByExtensionListViewSorter.HeaderClick(sender);
    }

    private void LargeFiles_ListView_ColumnHeader_Click(object sender, RoutedEventArgs e) {
      _largeFilesListViewSorter.HeaderClick(sender);
    }

    private void BinaryFilesByExtension_ListView_ColumnHeader_Click(object sender, RoutedEventArgs e) {
      _binaryFilesByExtensionListViewSorter.HeaderClick(sender);
    }

    private void LargeBinaryFiles_ListView_ColumnHeader_Click(object sender, RoutedEventArgs e) {
      _largeBinaryFilesListViewSorter.HeaderClick(sender);
    }

    public class ListViewSorter {
      private readonly ListView _listView;
      private bool _firstLoad = true;
      private GridViewColumnHeader _currentSortedColumnHeader;
      private SortAdorner _currentAdorner;

      public ListViewSorter(ListView listView) {
        _listView = listView;
        _listView.Loaded += (sender, args) => OnListViewLoaded();
      }

      private void OnListViewLoaded() {
        if (!_firstLoad) {
          return;
        }

        var gridView = _listView.View as GridView;
        if (gridView == null) {
          return;
        }

        foreach (var column in gridView.Columns) {
          var header = column.Header as GridViewColumnHeader;
          if (header != null) {
            // The "Loaded" event is called multiple times for UI elements inside
            // a tab control. The first "Loaded" event is called when the control
            // is not visible yet, meaning there are no adornment layers for the column
            // headers of the listview. The next "Loaded" events are called when
            // the containing TabItem is activated. We use the trick below
            // to figure out when it is safe to add the "Sorter" adornment, but
            // we must do it only once.
            if (AdornerLayer.GetAdornerLayer(header) != null) {
              _firstLoad = false;
              if (Wpf.ListViewSorter.GetInitialSortColumn(header)) {
                HeaderClick(header);
              }
            }
          }
        }
      }

      public void HeaderClick(object sender) {
        HeaderClick(sender as GridViewColumnHeader);
      }

      public void HeaderClick(GridViewColumnHeader column) {
        if (column == null) {
          return;
        }
        var sortBy = column.Tag.ToString();
        if (_currentSortedColumnHeader != null) {
          AdornerLayer.GetAdornerLayer(_currentSortedColumnHeader).Remove(_currentAdorner);
          _listView.Items.SortDescriptions.Clear();
        }

        var newDir = Wpf.ListViewSorter.GetInitialSortOrder(column);
        if (_currentSortedColumnHeader == column && _currentAdorner.Direction == newDir) {
          newDir = newDir == ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        }

        _currentSortedColumnHeader = column;
        _currentAdorner = new SortAdorner(_currentSortedColumnHeader, newDir);
        AdornerLayer.GetAdornerLayer(_currentSortedColumnHeader).Add(_currentAdorner);
        _listView.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
      }
    }

    public class SortAdorner : Adorner {
      private static Geometry ascGeometry =
        Geometry.Parse("M 0 4 L 3.5 0 L 7 4 Z");

      private static Geometry descGeometry =
        Geometry.Parse("M 0 0 L 3.5 4 L 7 0 Z");

      public ListSortDirection Direction { get; private set; }

      public SortAdorner(UIElement element, ListSortDirection dir)
        : base(element) {
        Direction = dir;
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
        if (Direction == ListSortDirection.Descending)
          geometry = descGeometry;
        drawingContext.DrawGeometry(Brushes.Black, null, geometry);

        drawingContext.Pop();
      }
    }
  }
}
