// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VsChromiumCore.Linq;

namespace VsChromiumPackage.ToolWindows.FileExplorer {
  public class LazyObservableCollection<T> : ObservableCollection<T>, ILazyObservableCollection<T> where T : class {
    private readonly int _lazyCount;
    private readonly Func<T> _lazyItemFactory;
    private readonly List<T> _lazyItems = new List<T>();
    private bool _lazy;
    private T _lazyItem;

    public LazyObservableCollection(int lazyCount, Func<T> lazyItemFactory) {
      this._lazyCount = lazyCount;
      this._lazyItemFactory = lazyItemFactory;
      this._lazy = true;
    }

    public T ExpandLazyNode() {
      T result = default(T);
      if (this._lazy) {
        this._lazy = false;
        if (Count >= this._lazyCount) {
          base.RemoveAt(this._lazyCount);
        }

        this._lazyItems.ForAll(x => Add(x));
        this._lazyItems.Clear();

        if (Count >= this._lazyCount) {
          result = base[this._lazyCount];
        }
      }
      return result;
    }

    protected override void ClearItems() {
      base.ClearItems();
      this._lazyItems.Clear();
      this._lazy = true;
    }

    protected override void InsertItem(int index, T item) {
      // Inserting the "lazy" item is a no-op from our point of view.
      if (object.ReferenceEquals(item, this._lazyItem)) {
        base.InsertItem(index, item);
        return;
      }

      // Insert lazy item entry at the end
      if (this._lazy && Count == this._lazyCount) {
        this._lazyItem = this._lazyItemFactory();
        base.Add(this._lazyItem);
        this._lazyItems.Add(item);
        return;
      }

      // If adding past the "lazt item", add to our lazy item list.
      if (this._lazy && Count >= this._lazyCount) {
        this._lazyItems.Add(item);
        return;
      }

      base.InsertItem(index, item);
    }
  }
}
