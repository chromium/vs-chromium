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
      var result = PathHelpers.IsPrefix(@"d:", @"d:\ooo\bar.txt");
      Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsPrefixWithPartialPrefixFails() {
      var result = PathHelpers.IsPrefix(@"d:\foo\foo", @"d:\foo\foo.bar");
      Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsPrefixWithPartialPrefixFails2() {
      var result = PathHelpers.IsPrefix(@"d:\foo\foo\", @"d:\foo\foo.bar");
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

    [TestMethod]
    public void CombinePathsAcceptsNullArgument() {
      var path = @"d:\ooo\bar.txt";
      var result = PathHelpers.CombinePaths(path, null);
      Assert.AreEqual(path, result);
    }

    [TestMethod]
    public void CombinePathsAcceptsEmptyArgument() {
      var path = @"d:\ooo\bar.txt";
      var result = PathHelpers.CombinePaths(path, "");
      Assert.AreEqual(path, result);
    }

    [TestMethod]
    public void CombinePathsAcceptsNullFirstArgument() {
      var path = @"d:\ooo\bar.txt";
      var result = PathHelpers.CombinePaths(null, path);
      Assert.AreEqual(path, result);
    }

    [TestMethod]
    public void CombinePathsAcceptsEmptyFirstArgument() {
      var path = @"d:\ooo\bar.txt";
      var result = PathHelpers.CombinePaths("", path);
      Assert.AreEqual(path, result);
    }

    [TestMethod]
    public void CombinePathsAllowsLongPaths() {
      var path = @"d:\ooo\bar.txt";
      while (path.Length < 300) {
        path = path + @"\dirname";
      }
      var result = PathHelpers.CombinePaths(path, "myfile.txt");
      Assert.AreEqual(path + @"\myfile.txt", result);
    }
  }
}
