// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests {
  [TestClass]
  public class TestPathUtils {
    [TestMethod]
    public void CustomPathComparerWorks() {
      var comparer = CustomPathComparer.Instance;
      Assert.IsTrue(comparer.Compare(@"foo", @"foo") == 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar", @"foo\bar") == 0);
      Assert.IsTrue(comparer.Compare(@"foo/bar", @"foo/bar") == 0);
      Assert.IsTrue(comparer.Compare(@"foo/bar", @"foo/bar") == 0);
      Assert.IsTrue(comparer.Compare(@"foo/bar", @"foo\bar") == 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar2", @"foo\bar") > 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar", @"foo\bar2") < 0);
      Assert.IsTrue(comparer.Compare(@"foo1\bar", @"foo\bar") > 0);
      Assert.IsTrue(comparer.Compare(@"foo-1\bar", @"foo\bar") > 0);
      Assert.IsTrue(comparer.Compare(@"foo\bar", @"foo-1\bar") < 0);
    }
  }
}
