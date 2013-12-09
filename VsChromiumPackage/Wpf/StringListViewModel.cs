// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VsChromiumPackage.Wpf {
  public class StringListViewModel : INotifyPropertyChanged {
    private readonly ObservableCollection<string> _items = new ObservableCollection<string>();
    private string _selectedItem;

    public IEnumerable<string> Items {
      get {
        return this._items;
      }
    }

    public string SelectedItem {
      get {
        return this._selectedItem;
      }
      set {
        if (this._selectedItem == value)
          return;

        this._selectedItem = value;
        OnPropertyChanged("SelectedItem");
      }
    }

    /// <summary>
    /// Called when updating the data source. We add the item in the list of items
    /// if we don't have it yet.
    /// </summary>
    public string NewItem {
      set {
        if (string.IsNullOrEmpty(value))
          return;

        if (!this._items.Contains(value))
          this._items.Insert(0, value);

        SelectedItem = value;
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged(string propertyName) {
      var handler = PropertyChanged;
      if (handler != null)
        handler(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
