﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;

namespace VsChromium.Wpf {
  public class StringListViewModel : INotifyPropertyChanged {
    private readonly ObservableCollection<string> _items = new ObservableCollection<string>();
    private string _selectedItem;

    public StringListViewModel() {
    }

    public StringListViewModel(IEnumerable<string> initialItems) {
      initialItems.ForAll(_items.Add);
    }

    public ICollection<string> Items {
      get {
        return _items;
      }
    }

    public string SelectedItem {
      get { return _selectedItem; }
      set {
        if (_selectedItem == value)
          return;

        _selectedItem = value;
        OnPropertyChanged(ReflectionUtils.GetPropertyName(this, x => x.SelectedItem));
      }
    }

    /// <summary>
    /// Called when updating the data source. We add the item in the list of items
    /// if we don't have it yet.
    /// </summary>
    public string NewItem {
      set {
        if (string.IsNullOrEmpty(value)) {
          SelectedItem = null;
          return;
        }

        if (!_items.Contains(value))
          _items.Insert(0, value);

        SelectedItem = value;
      }
      get {
        return _selectedItem;
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
