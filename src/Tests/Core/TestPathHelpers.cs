// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestPathHelpers {
    [TestMethod]
    public void IsPrefixWorks() {
      var result = PathHelpers.IsPrefix(@"d:\ooo\bar.txt", @"d:");
      Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsPrefixWithPartialPrefixFails() {
      var result = PathHelpers.IsPrefix(@"d:\foo\foo.bar", @"d:\foo\foo");
      Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsPrefixWithPartialPrefixFails2() {
      var result = PathHelpers.IsPrefix(@"d:\foo\foo.bar", @"d:\foo\foo\");
      Assert.IsFalse(result);
    }

    [TestMethod]
    public void SplitPrefixRemovesSeparators() {
      var result = PathHelpers.SplitPrefix(@"d:\ooo\bar.txt", @"d:");
      Assert.AreEqual(@"d:", result.Root);
      Assert.AreEqual(@"ooo\bar.txt", result.Suffix);
    }

    [TestMethod]
    public void SplitPrefixRemovesSeparators2() {
      var result = PathHelpers.SplitPrefix(@"d:\ooo\bar.txt", @"d:\ooo");
      Assert.AreEqual(@"d:\ooo", result.Root);
      Assert.AreEqual(@"bar.txt", result.Suffix);
    }

    [TestMethod]
    public void SplitPrefixRemovesTrailingSeparators() {
      var result = PathHelpers.SplitPrefix(@"d:\ooo\bar.txt", @"d:\");
      Assert.AreEqual(@"d:", result.Root);
      Assert.AreEqual(@"ooo\bar.txt", result.Suffix);
    }
    [TestMethod]
    public void SplitPrefixRemovesTrailingSeparators2() {
      var result = PathHelpers.SplitPrefix(@"d:\ooo\bar.txt", @"d:\ooo\");
      Assert.AreEqual(@"d:\ooo", result.Root);
      Assert.AreEqual(@"bar.txt", result.Suffix);
    }
  }
}
