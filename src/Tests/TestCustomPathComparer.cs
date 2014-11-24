// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests {
  [TestClass]
  public class TestCustomPathComparer {
    [TestMethod]
    public void CustomPathComparerWorks() {
      var comparer = new CustomPathComparer(PathComparisonOption.CaseInsensitive);
      Assert.IsTrue(comparer.Compare(@"foo", @"foo") == 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar", @"foo\bar") == 0);
      Assert.IsTrue(comparer.Compare(@"foobar", @"foo\bar") > 0);
      Assert.IsTrue(comparer.Compare(@"foo", @"foobar\bar") < 0);
      Assert.IsTrue(comparer.Compare(@"foo", @"foo\bar") < 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar", @"foo") > 0);
      Assert.IsTrue(comparer.Compare(@"xoo", @"foo\bar") > 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar", @"xoo") < 0);
      Assert.IsTrue(comparer.Compare(@"foo", @"xoo\bar") < 0);
      Assert.IsTrue(comparer.Compare(@"xoo\bar", @"foo") > 0);
      Assert.IsTrue(comparer.Compare(@"foo/bar", @"foo/bar") == 0);
      Assert.IsTrue(comparer.Compare(@"foo/bar", @"foo/bar") == 0);
      Assert.IsTrue(comparer.Compare(@"foo/bar", @"foo\bar") == 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar2", @"foo\bar") > 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar", @"foo\bar2") < 0);
      Assert.IsTrue(comparer.Compare(@"foo1\bar", @"foo\bar") > 0);
      Assert.IsTrue(comparer.Compare(@"foo-1\bar", @"foo\bar") > 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar", @"foo-1\bar") < 0);
      Assert.IsTrue(comparer.Compare(@"axiom-shell\out\chrome_app\polymer\axiom_view_manager.js", @"test-apps\venkman\view_manager.ts") < 0);
    }
  }
}
