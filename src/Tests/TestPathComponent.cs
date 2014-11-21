// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests {
  [TestClass]
  public class TestPathComponent {
    [TestMethod]
    public void PathComponentEqualityWorks() {
      var p1 = new PathComponent("foo", 0, 3, PathComparisonOption.CaseInsensitive);
      var p2 = new PathComponent("Foo", 0, 3, PathComparisonOption.CaseInsensitive);
      Assert.AreEqual(p1, p2);
    }

    [TestMethod]
    public void PathComponentEqualityWorks2() {
      var p1 = new PathComponent("foo", 0, 3, PathComparisonOption.CaseSensitive);
      var p2 = new PathComponent("Foo", 0, 3, PathComparisonOption.CaseSensitive);
      Assert.AreNotEqual(p1, p2);
    }

    [TestMethod]
    public void PathComponentHashCodeWorks() {
      var p1 = new PathComponent("foo", 0, 3, PathComparisonOption.CaseInsensitive);
      var p2 = new PathComponent("Foo", 0, 3, PathComparisonOption.CaseInsensitive);
      Assert.AreEqual(p1.GetHashCode(), p2.GetHashCode());
    }

    [TestMethod]
    public void PathComponentHashCodeWorks2() {
      var p1 = new PathComponent("foo", 0, 3, PathComparisonOption.CaseSensitive);
      var p2 = new PathComponent("Foo", 0, 3, PathComparisonOption.CaseSensitive);
      Assert.AreNotEqual(p1.GetHashCode(), p2.GetHashCode());
    }
  }
}
