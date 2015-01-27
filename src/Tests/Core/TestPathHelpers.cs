// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestPathHelpers {
    [TestMethod]
    public void PathHelpersWorks() {
      var result = PathHelpers.SplitPath(@"d:\ooo\bar.txt", @"d:\");
      Assert.AreEqual(@"d:", result.Key);
      Assert.AreEqual(@"ooo\bar.txt", result.Value);
    }
    [TestMethod]
    public void PathHelpersWorks2() {
      var result = PathHelpers.SplitPath(@"d:\ooo\bar.txt", @"d:");
      Assert.AreEqual(@"d:", result.Key);
      Assert.AreEqual(@"ooo\bar.txt", result.Value);
    }
  }
}
