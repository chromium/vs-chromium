// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Windows.Forms;

namespace VsChromium.Features.AttachToChrome {
  class ColumnSorter : IComparer {
    private int _sortColumn;
    private SortOrder _direction;
    private CaseInsensitiveComparer _stringComparer;
    private NumericStringComparer _numericComparer;

    public ColumnSorter() {
      _sortColumn = 0;
      _direction = SortOrder.None;
      _stringComparer = new CaseInsensitiveComparer();
      _numericComparer = new NumericStringComparer();
    }

    public int Compare(object x, object y) {
      ProcessViewItem xitem = (ProcessViewItem)x;
      ProcessViewItem yitem = (ProcessViewItem)y;

      IComparer comparer = _stringComparer;
      if (_sortColumn == 1)
        comparer = _numericComparer;
      int result = comparer.Compare(
          xitem.SubItems[_sortColumn].Text,
          yitem.SubItems[_sortColumn].Text);

      if (_direction == SortOrder.Ascending)
        return result;
      else if (_direction == SortOrder.Descending)
        return -result;
      else
        return 0;
    }

    public int SortColumn {
      get { return _sortColumn; }
      set { _sortColumn = value; }
    }

    public SortOrder Direction {
      get { return _direction; }
      set { _direction = value; }
    }
  }
}
