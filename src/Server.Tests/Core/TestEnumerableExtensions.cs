// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using VsChromium.Core.Linq;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestEnumerableExtensions {
    [TestMethod]
    public void TakeOrderByDescendingWorks() {
      var numbers = new[] { 5, 6, 7, 1, 8, 9, 10, 3, 13, 14, 2 };
      var o1 = numbers.OrderByDescendingThenTake(5, x => x).ToList();

      Assert.AreEqual(5, o1.Count);
      Assert.AreEqual(14, o1[0]);
      Assert.AreEqual(13, o1[1]);
      Assert.AreEqual(10, o1[2]);
      Assert.AreEqual(9, o1[3]);
      Assert.AreEqual(8, o1[4]);
    }
  }
}
