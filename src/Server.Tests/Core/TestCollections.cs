// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Collections;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestCollections {
    [TestMethod]
    public void HeapWorks() {
      var heap = new MaxHeap<int>(6, Comparer<int>.Default);
      heap.Add(5);
      heap.Add(6);
      heap.Add(4);
      heap.Add(1);
      heap.Add(-1);

      Assert.AreEqual(6, heap.Remove());
      Assert.AreEqual(5, heap.Remove());
      Assert.AreEqual(4, heap.Remove());
      Assert.AreEqual(1, heap.Remove());
      Assert.AreEqual(-1, heap.Remove());
    }
  }
}
