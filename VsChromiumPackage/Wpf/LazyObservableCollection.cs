// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VsChromium.Core.Linq;

namespace VsChromium.Wpf {
  public class LazyObservableCollection<T> : ObservableCollection<T>, ILazyObservableCollection<T> where T : class {
    private readonly int _lazyCount;
    private readonly Func<T> _lazyItemFactory;
    private readonly List<T> _lazyItems = new List<T>();
    private bool _lazy;
    private T _lazyItem;

    public LazyObservableCollection(int lazyCount, Func<T> lazyItemFactory) {
      _lazyCount = lazyCount;
      _lazyItemFactory = lazyItemFactory;
      _lazy = true;
    }

    public T ExpandLazyNode() {
      T result = default(T);
      if (_lazy) {
        _lazy = false;
        if (Count >= _lazyCount) {
          base.RemoveAt(_lazyCount);
        }

        _lazyItems.ForAll(x => Add(x));
        _lazyItems.Clear();

        if (Count >= _lazyCount) {
          result = base[_lazyCount];
        }
      }
      return result;
    }

    protected override void ClearItems() {
      base.ClearItems();
      _lazyItems.Clear();
      _lazy = true;
    }

    protected override void InsertItem(int index, T item) {
      // Inserting the "lazy" item is a no-op from our point of view.
      if (object.ReferenceEquals(item, _lazyItem)) {
        base.InsertItem(index, item);
        return;
      }

      // Insert lazy item entry at the end
      if (_lazy && Count == _lazyCount) {
        _lazyItem = _lazyItemFactory();
        base.Add(_lazyItem);
        _lazyItems.Add(item);
        return;
      }

      // If adding past the "lazt item", add to our lazy item list.
      if (_lazy && Count >= _lazyCount) {
        _lazyItems.Add(item);
        return;
      }

      base.InsertItem(index, item);
    }
  }
}
