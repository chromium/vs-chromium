using System;
using System.Diagnostics;
using System.Linq;

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
      Debug.Assert(found);
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
      Debug.Assert(false);
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

      while (item != null) {
        result = NextSibling(item);
        if (result != null)
          return result;
        item = item.GetParent();
      }
      return null;
    }

    /// <summary>
    /// Returns the item (or null) directly following <paramref name="item"/> in
    /// "previous sibling, parent" order.
    /// </summary>
    public IHierarchyObject GetPreviousItem(IHierarchyObject item) {
      if (item == null)
        return null;

      var result = PreviousSibling(item);
      if (result == null)
        return item.GetParent();

      while (true) {
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
      while (item != null) {
        item = apply(item);
        var result = item as T;
        if (result != null)
          return result;
      }

      return null;
    }
  }
}