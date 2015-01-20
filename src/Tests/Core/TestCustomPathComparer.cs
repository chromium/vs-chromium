// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestCustomPathComparer {
    [TestMethod]
    public void CustomPathComparerWorks() {
      var comparer = new CustomPathComparer(PathComparisonOption.CaseInsensitive);
      Assert.IsTrue(Compare(comparer, @"foo", @"foo") == 0);
      Assert.IsTrue(Compare(comparer, @"foo/bar", @"foo/bar") == 0);
      Assert.IsTrue(Compare(comparer, @"foobar", @"foo/bar") > 0);
      Assert.IsTrue(Compare(comparer, @"foo", @"foobar/bar") < 0);
      Assert.IsTrue(Compare(comparer, @"foo", @"foo/bar") < 0);
      Assert.IsTrue(Compare(comparer, @"foo/bar", @"foo") > 0);
      Assert.IsTrue(Compare(comparer, @"xoo", @"foo/bar") > 0);
      Assert.IsTrue(Compare(comparer, @"foo/bar", @"xoo") < 0);
      Assert.IsTrue(Compare(comparer, @"foo", @"xoo/bar") < 0);
      Assert.IsTrue(Compare(comparer, @"xoo/bar", @"foo") > 0);
      Assert.IsTrue(Compare(comparer, @"foo/bar", @"foo/bar") == 0);
      Assert.IsTrue(Compare(comparer, @"foo/bar2", @"foo/bar") > 0);
      Assert.IsTrue(Compare(comparer, @"foo/bar", @"foo/bar2") < 0);
      Assert.IsTrue(Compare(comparer, @"foo1/bar", @"foo/bar") > 0);
      Assert.IsTrue(Compare(comparer, @"foo-1/bar", @"foo/bar") > 0);
      Assert.IsTrue(Compare(comparer, @"foo/bar", @"foo-1/bar") < 0);
      Assert.IsTrue(Compare(comparer, @"axiom-shell/out/chrome_app/polymer/axiom_view_manager.js", @"test-apps/venkman/view_manager.ts") < 0);
    }

    public int Compare(CustomPathComparer comparer, string x, string y) {
      x = x.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
      y = y.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
      return comparer.Compare(x, y);
    }

    [TestMethod]
    public void CustomPathComparerWorks2() {
      var comparer = new CustomPathComparer(PathComparisonOption.CaseInsensitive);
      Assert.IsTrue(comparer.Compare(@"foo", 0, @"foo", 0, 1) == 0);
      Assert.IsTrue(comparer.Compare(@"foo", 0, @"foo", 0, 2) == 0);
      Assert.IsTrue(comparer.Compare(@"foo", 0, @"foo", 0, 3) == 0);
      Assert.IsTrue(comparer.Compare(@"1foo", 1, @"12foo", 2, 3) == 0);
      Assert.IsTrue(comparer.Compare(@"12foo", 2, @"1foo", 1, 3) == 0);
      Assert.IsTrue(comparer.Compare(@"12foo", 2, @"foo", 0, 3) == 0);
      Assert.IsTrue(comparer.Compare(@"1foo12", 1, @"12foo12", 2, 3) == 0);
      Assert.IsTrue(comparer.Compare(@"12foo12", 2, @"1foo12", 1, 3) == 0);
      Assert.IsTrue(comparer.Compare(@"12foo12", 2, @"foo12", 0, 3) == 0);
      Assert.IsTrue(comparer.Compare(@"1foo\bar12", 1, @"12foo\bar12", 2, 7) == 0);
      Assert.IsTrue(comparer.Compare(@"12foo\bar12", 2, @"1foo\bar12", 1, 7) == 0);
      Assert.IsTrue(comparer.Compare(@"12foo\bar12", 2, @"foo\bar12", 0, 7) == 0);
      Assert.IsTrue(comparer.Compare(@"xoo", 0, @"foo\xxx", 0, 7) > 0);
      Assert.IsTrue(comparer.Compare(@"xoo", 0, @"foo\xxx", 4, 3) < 0);
    }
  }
}
