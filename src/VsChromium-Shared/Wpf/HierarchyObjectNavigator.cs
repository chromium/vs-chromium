// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Wpf {
  public class HierarchyObjectNavigator {
    private IHierarchyObject FirstChild(IHierarchyObject item) {
      if (item == null)
        return null;
      return item.GetAllChildren().FirstOrDefault();
    }

    private IHierarchyObject LastChild(IHierarchyObject item) {
      if (item == null)
        return null;
      return item.GetAllChildren().LastOrDefault();
    }

    private IHierarchyObject NextSibling(IHierarchyObject item) {
      if (item == null)
        return null;
      var parent = item.GetParent();
      if (parent == null)
        return null;

      bool found = false;
      foreach (var child in parent.GetAllChildren()) {
        if (found)
          return child;
        if (child.Equals(item))
          found = true;
      }
      Invariants.Assert(found);
      return null;
    }

    private IHierarchyObject PreviousSibling(IHierarchyObject item) {
      if (item == null)
        return null;
      var parent = item.GetParent();
      if (parent == null)
        return null;

      IHierarchyObject previous = null;
      foreach (var child in parent.GetAllChildren()) {
        if (child.Equals(item))
          return previous;
        previous = child;
      }
      Invariants.Assert(false);
      return null;
    }

    /// <summary>
    /// Returns the item (or null) directly following <paramref name="item"/> in
    /// "first child, next sibling" order.
    /// </summary>
    public IHierarchyObject GetNextItem(IHierarchyObject item) {
      if (item == null)
        return null;

      // First child if any
      var result = FirstChild(item);
      if (result != null)
        return result;

      // a
      //   b
      //     c
      //     d
      // e
      //
      // Return "b" is we are on "a"
      // Return "d" if we are on "c"
      // Return "e" if we are on "d"
      // Return "a" if we are on "e"
      //
      while (true) {
        Invariants.Assert(item != null);
        result = NextSibling(item);
        if (result != null)
          return result;
        var parent = item.GetParent();
        if (parent == null)
          return item;
        item = parent;
      }
    }

    /// <summary>
    /// Returns the item (or null) directly following <paramref name="item"/> in
    /// "previous sibling, parent" order.
    /// </summary>
    public IHierarchyObject GetPreviousItem(IHierarchyObject item) {
      if (item == null)
        return null;

      // a
      //   b
      //     c
      //     d
      // e
      //
      // Return "b" is we are on "c"
      // Return "d" if we are on "e"
      // Return "e" if we are on "a"
      // Return "a" if we are on "b"
      //
      var result = PreviousSibling(item);
      if (result == null) {
        var parent = item.GetParent();
        if (parent != null)
          return parent;
        // "a" case: get the very last child at the bottom of the tree
        while (true) {
          Invariants.Assert(item != null);
          var last = LastChild(item);
          if (last == null)
            return item;
          item = last;
        }
       }

      while (true) {
        Invariants.Assert(result != null);
        var last = LastChild(result);
        if (last == null)
          return result;
        result = last;
      }
    }

    /// <summary>
    /// Returns the item (or null) directly following <paramref name="item"/> in
    /// "first child, next sibling" order until a node of type <typeparamref
    /// name="T"/> is hit.
    /// </summary>
    public T GetNextItemOfType<T>(IHierarchyObject item) where T : class, IHierarchyObject {
      return GetItemOfType<T>(GetNextItem, item);
    }

    /// <summary>
    /// Returns the item (or null) directly following <paramref name="item"/> in
    /// "previous sibling, parent" order until a node of type <typeparamref
    /// name="T"/> is hit.
    /// </summary>
    public T GetPreviousItemOfType<T>(IHierarchyObject item) where T : class, IHierarchyObject {
      return GetItemOfType<T>(GetPreviousItem, item);
    }

    public T GetItemOfType<T>(Func<IHierarchyObject, IHierarchyObject> apply, IHierarchyObject item) where T : class, IHierarchyObject {
      var initialItem = item;
      while (item != null) {
        item = apply(item);
        if (ReferenceEquals(item, initialItem))
          break;
        var result = item as T;
        if (result != null)
          return result;
      }
      return null;
    }
  }
}