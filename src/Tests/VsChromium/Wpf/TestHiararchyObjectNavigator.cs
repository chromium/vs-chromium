// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Wpf;

namespace VsChromium.Tests.VsChromium.Wpf {
  [TestClass]
  public class TestHiararchyObjectNavigator {
    [TestMethod]
    public void GetNextEntryWorks() {
      var tree = new HierarchyObjectMock("root",
        new HierarchyObjectMock("child1",
          new HierarchyObjectMock("child1-1"),
          new HierarchyObjectMock("child1-2",
            new HierarchyObjectMock("child1-2-1"),
            new HierarchyObjectMock("child1-2-2",
              new HierarchyObjectMock("child1-2-2-1"))),
          new HierarchyObjectMock("child1-3",
            new HierarchyObjectMock("child1-3-1")),
          new HierarchyObjectMock("child1-4")));

      var navigator = new HierarchyObjectNavigator();
      Assert.AreEqual("child1", GetNextItem(navigator, tree, "root"));
      Assert.AreEqual("child1-1", GetNextItem(navigator, tree, "child1"));
      Assert.AreEqual("child1-2", GetNextItem(navigator, tree, "child1-1"));
      Assert.AreEqual("child1-2-1", GetNextItem(navigator, tree, "child1-2"));
      Assert.AreEqual("child1-2-2", GetNextItem(navigator, tree, "child1-2-1"));
      Assert.AreEqual("child1-3", GetNextItem(navigator, tree, "child1-2-2-1"));
      Assert.AreEqual("child1-4", GetNextItem(navigator, tree, "child1-3-1"));
      Assert.AreEqual(null, GetNextItem(navigator, tree, "child1-4"));
    }

    [TestMethod]
    public void GetPreviousEntryWorks() {
      var tree = new HierarchyObjectMock("root",
        new HierarchyObjectMock("child1",
          new HierarchyObjectMock("child1-1"),
          new HierarchyObjectMock("child1-2",
            new HierarchyObjectMock("child1-2-1"),
            new HierarchyObjectMock("child1-2-2",
              new HierarchyObjectMock("child1-2-2-1"))),
          new HierarchyObjectMock("child1-3",
            new HierarchyObjectMock("child1-3-1")),
          new HierarchyObjectMock("child1-4")));

      var navigator = new HierarchyObjectNavigator();
      Assert.AreEqual(null, GetPreviousItem(navigator, tree, "root"));
      Assert.AreEqual("root", GetPreviousItem(navigator, tree, "child1"));
      Assert.AreEqual("child1", GetPreviousItem(navigator, tree, "child1-1"));
      Assert.AreEqual("child1-1", GetPreviousItem(navigator, tree, "child1-2"));
      Assert.AreEqual("child1-2", GetPreviousItem(navigator, tree, "child1-2-1"));
      Assert.AreEqual("child1-2-2", GetPreviousItem(navigator, tree, "child1-2-2-1"));
      Assert.AreEqual("child1-2-2-1", GetPreviousItem(navigator, tree, "child1-3"));
      Assert.AreEqual("child1-3", GetPreviousItem(navigator, tree, "child1-3-1"));
      Assert.AreEqual("child1-3-1", GetPreviousItem(navigator, tree, "child1-4"));
    }

    private HierarchyObjectMock FindNode(HierarchyObjectMock root, string value) {
      if (root == null)
        return null;
      if (root.Value == value)
        return root;
      foreach (var child in root.ChildMocks) {
        var result = FindNode(child, value);
        if (result != null)
          return result;
      }
      return null;
    }

    private string GetNextItem(HierarchyObjectNavigator navigator, HierarchyObjectMock root, string value) {
      var node = FindNode(root, value);
      if (node == null)
        return null;
      return GetNextItem(navigator, node);
    }

    private string GetNextItem(HierarchyObjectNavigator navigator, HierarchyObjectMock tree) {
      var item = navigator.GetNextItem(tree) as HierarchyObjectMock;
      if (item == null)
        return null;
      return item.Value;
    }

    private string GetPreviousItem(HierarchyObjectNavigator navigator, HierarchyObjectMock root, string value) {
      var node = FindNode(root, value);
      if (node == null)
        return null;
      return GetPreviousItem(navigator, node);
    }

    private string GetPreviousItem(HierarchyObjectNavigator navigator, HierarchyObjectMock tree) {
      var item = navigator.GetPreviousItem(tree) as HierarchyObjectMock;
      if (item == null)
        return null;
      return item.Value;
    }

    public class HierarchyObjectMock : IHierarchyObject {
      private readonly string _value;
      private readonly IList<HierarchyObjectMock> _children;
      private IHierarchyObject _parent;

      public HierarchyObjectMock(string value, params HierarchyObjectMock[] children) {
        _value = value;
        _children = children;
        foreach (var child in children)
          child._parent = this;
      }
      public string Value { get { return _value; } }

      public bool IsVisual { get { return true; } }

      public IHierarchyObject GetParent() {
        return _parent;
      }

      public IList<IHierarchyObject> GetAllChildren() {
        return _children.Cast<IHierarchyObject>().ToList();
      }

      public IList<HierarchyObjectMock> ChildMocks {
        get { return _children; }
      }
    }
  }
}
